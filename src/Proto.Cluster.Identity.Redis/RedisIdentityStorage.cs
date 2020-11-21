using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Proto.Cluster.Identity.Redis
{
    public class RedisIdentityStorage : IIdentityStorage
    {
        private static readonly ILogger Logger = Log.CreateLogger<RedisIdentityStorage>();

        private readonly RedisKey _clusterIdentityKey;
        private readonly RedisKey _memberKey;
        private static readonly RedisKey NoKey = new();

        private static readonly RedisValue UniqueIdentity = "pid";
        private static readonly RedisValue Address = "adr";
        private static readonly RedisValue MemberId = "mid";
        private static readonly RedisValue LockId = "lid";

        private readonly IConnectionMultiplexer _connections;

        private IDatabase GetDb() => _connections.GetDatabase();

        public RedisIdentityStorage(string clusterName, IConnectionMultiplexer connections)
        {
            RedisKey baseKey = clusterName + ":";
            _clusterIdentityKey = baseKey.Append("ci:");
            _memberKey = baseKey.Append("mb:");
            _connections = connections;
        }

        public async Task<SpawnLock?> TryAcquireLockAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var requestId = Guid.NewGuid().ToString();
            var hasLock = await TryAcquireLockAsync(clusterIdentity, requestId);

            Logger.LogDebug(
                hasLock ? "Took lock on {@ClusterIdentity}" : "Did not get lock on {@ClusterIdentity}",
                clusterIdentity
            );

            return hasLock ? new SpawnLock(requestId, clusterIdentity) : null;
        }

        public async Task<StoredActivation?> WaitForActivationAsync(
            ClusterIdentity clusterIdentity,
            CancellationToken ct
        )
        {
            var key = IdKey(clusterIdentity);
            var db = GetDb();
            var activation = await LookupKey(db, key);
            var i = 1;

            while (activation == null && !ct.IsCancellationRequested) {
                await Task.Delay(20 * i++, ct);
                activation = await LookupKey(db, key);
            }

            Logger.LogDebug(
                "After waiting {Iteration} times, got {@Activation} for {@ClusterIdentity}",
                i,
                activation,
                clusterIdentity
            );

            return activation;
        }

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct)
        {
            Logger.LogDebug("Deleting spawn lock {@SpawnLock}", spawnLock.ClusterIdentity);
            var db = GetDb();

            var key = IdKey(spawnLock.ClusterIdentity);
            var transaction = db.CreateTransaction();
            transaction.AddCondition(Condition.HashEqual(key, LockId, spawnLock.LockId));
            transaction.HashDeleteAsync(key, LockId);
            transaction.Execute(CommandFlags.FireAndForget);
            return Task.CompletedTask;
        }

        public Task StoreActivation(
            string memberId, SpawnLock spawnLock, PID pid,
            CancellationToken ct
        )
        {
            Logger.LogDebug(
                "Storing activation {@Activation} for {@ClusterIdentity} on member {MemberId}",
                pid,
                spawnLock.ClusterIdentity,
                memberId
            );

            var key = IdKey(spawnLock.ClusterIdentity);

            var db = GetDb();

            var values = new[] {
                new HashEntry(UniqueIdentity, pid.Id),
                new HashEntry(Address, pid.Address),
                new HashEntry(MemberId, memberId),
                new HashEntry(LockId, RedisValue.EmptyString),
            };

            return Task.WhenAll(
                db.HashSetAsync(key, values, CommandFlags.DemandMaster),
                db.SetAddAsync(MemberKey(memberId), pid.Id, CommandFlags.DemandMaster)
            );
        }

        public async Task RemoveActivation(PID pid, CancellationToken ct)
        {
            Logger.LogDebug("Removing activation: {@PID}", pid);

            var key = IdKeyFromPidId(pid.Id);
            if (key == NoKey) return;

            var db = GetDb();
            var activation = await LookupKey(db, key);

            if (activation == null || activation.Pid.Equals(pid) == false) return;

            var memberKey = MemberKey(activation.MemberId);
            var transaction = db.CreateTransaction();

            transaction.AddCondition(Condition.HashEqual(key, UniqueIdentity, pid.Id));
            _ = transaction.KeyDeleteAsync(key);
            _ = transaction.SetRemoveAsync(memberKey, pid.Id);
            await transaction.ExecuteAsync();
        }

        public async Task RemoveMemberIdAsync(string memberId, CancellationToken ct)
        {
            var key = MemberKey(memberId);

            var db = GetDb();
            //TODO: Consider rewriting as serverside script.
            var pidIds = db.SetScanAsync(key);

            var transactionsFinished = new List<Task>();

            await foreach (var pidId in pidIds.WithCancellation(ct)) {
                var pidKey = IdKeyFromPidId(pidId);
                if (pidKey == NoKey) continue;

                var transaction = db.CreateTransaction();
                transaction.AddCondition(Condition.HashEqual(pidKey, UniqueIdentity, pidId));
                _ = transaction.KeyDeleteAsync(pidKey);
                transactionsFinished.Add(transaction.ExecuteAsync());
            }

            await Task.WhenAll(transactionsFinished);
        }

        public Task<StoredActivation?> TryGetExistingActivationAsync(
            ClusterIdentity clusterIdentity,
            CancellationToken ct
        )
            => LookupKey(GetDb(), IdKey(clusterIdentity));

        private Task<bool> TryAcquireLockAsync(
            ClusterIdentity clusterIdentity,
            string requestId
        )
        {
            var key = IdKey(clusterIdentity);

            return GetDb().HashSetAsync(key, LockId, requestId, When.NotExists, CommandFlags.DemandMaster);
        }

        private static async Task<StoredActivation?> LookupKey(IDatabaseAsync db, RedisKey key)
        {
            var result = await db.HashGetAllAsync(key);
            if (!(result?.Length > 2)) return null;

            var values = result.ToDictionary();

            return new StoredActivation(
                values[MemberId],
                PID.FromAddress(values[Address], values[UniqueIdentity])
            );
        }

        private RedisKey IdKey(ClusterIdentity clusterIdentity) => IdKey(clusterIdentity.ToShortString());

        private RedisKey IdKeyFromPidId(string pidId)
            => IdentityStorageLookup.TryGetClusterIdentityShortString(pidId, out var clusterId)
                ? IdKey(clusterId!)
                : NoKey;

        private RedisKey IdKey(string clusterIdentity) => _clusterIdentityKey.Append(clusterIdentity);

        private RedisKey MemberKey(string memberId) => _memberKey.Append(memberId);

        public void Dispose() => _connections.Dispose();
    }
}