using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Proto.Cluster.Identity.Redis
{
    public class RedisIdentityStorage : IIdentityStorage
    {
        private static readonly ILogger Logger = Log.CreateLogger<RedisIdentityStorage>();
        private static readonly Random Jitter = new();
        private static readonly RedisKey NoKey = new();

        private static readonly RedisValue UniqueIdentity = "pid";
        private static readonly RedisValue Address = "adr";
        private static readonly RedisValue MemberId = "mid";
        private static readonly RedisValue LockId = "lid";

        private readonly RedisKey _clusterIdentityKey;

        private readonly IConnectionMultiplexer _connections;
        private readonly TimeSpan _maxLockTime;
        private readonly RedisKey _memberKey;

        public RedisIdentityStorage(string clusterName, IConnectionMultiplexer connections,
            TimeSpan? maxWaitBeforeStaleLock = null)
        {
            RedisKey baseKey = clusterName + ":";
            _clusterIdentityKey = baseKey.Append("ci:");
            _memberKey = baseKey.Append("mb:");
            _connections = connections;
            _maxLockTime = maxWaitBeforeStaleLock ?? TimeSpan.FromSeconds(3);
        }

        public async Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var requestId = Guid.NewGuid().ToString();
            var hasLock = await TryAcquireLockAsync(clusterIdentity, requestId);

            Logger.LogDebug(
                hasLock ? "Took lock on {@ClusterIdentity}" : "Did not get lock on {@ClusterIdentity}",
                clusterIdentity
            );

            return hasLock ? new SpawnLock(requestId, clusterIdentity) : null;
        }

        public async Task<StoredActivation?> WaitForActivation(
            ClusterIdentity clusterIdentity,
            CancellationToken ct
        )
        {
            var timer = Stopwatch.StartNew();
            var key = IdKey(clusterIdentity);
            var db = GetDb();

            var activationStatus = await LookupKey(db, key);
            var lockId = activationStatus?.ActiveLockId;

            if (lockId != null)
            {
                //There is an active lock on the pid, spin wait
                var i = 0;
                do
                {
                    await Task.Delay(Jitter.Next(20) + 100 * i++, ct);
                } while ((activationStatus = await LookupKey(db, key))?.ActiveLockId == lockId &&
                         _maxLockTime > timer.Elapsed &&
                         !ct.IsCancellationRequested);
            }

            //the lookup entity was lost, stale lock maybe?
            if (activationStatus == null) return null;

            //lookup was unlocked, return this pid
            if (activationStatus.Activation != null)
                return activationStatus.Activation;

            //Still locked but not by the same request that originally locked it, so not stale
            if (activationStatus.ActiveLockId != lockId) return null;

            //Stale lock. just delete it and let cluster retry
            await RemoveLock(new SpawnLock(lockId!, clusterIdentity), CancellationToken.None);

            return null;
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

        public async Task StoreActivation(
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

            var values = new[]
            {
                new HashEntry(UniqueIdentity, pid.Id),
                new HashEntry(Address, pid.Address),
                new HashEntry(MemberId, memberId),
                new HashEntry(LockId, RedisValue.EmptyString)
            };

            var transaction = db.CreateTransaction();
            transaction.AddCondition(Condition.HashEqual(key, LockId, spawnLock.LockId));
            _ = transaction.HashSetAsync(key, values, CommandFlags.DemandMaster);
            _ = transaction.SetAddAsync(MemberKey(memberId), pid.Id, CommandFlags.DemandMaster);

            var executed = await transaction.ExecuteAsync();
            if (!executed) throw new LockNotFoundException($"Failed to store activation of {pid.ToShortString()}");
        }

        public async Task RemoveActivation(PID pid, CancellationToken ct)
        {
            Logger.LogDebug("Removing activation: {@PID}", pid);

            var key = IdKeyFromPidId(pid.Id);
            if (key == NoKey) return;

            var db = GetDb();
            var activationStatus = await LookupKey(db, key);

            if (activationStatus?.Activation?.Pid.Equals(pid) != true) return;

            var memberKey = MemberKey(activationStatus.Activation.MemberId);
            var transaction = db.CreateTransaction();

            transaction.AddCondition(Condition.HashEqual(key, UniqueIdentity, pid.Id));
            _ = transaction.KeyDeleteAsync(key);
            _ = transaction.SetRemoveAsync(memberKey, pid.Id);
            await transaction.ExecuteAsync();
        }

        public async Task RemoveMember(string memberId, CancellationToken ct)
        {
            var key = MemberKey(memberId);

            var db = GetDb();
            //TODO: Consider rewriting as serverside script.
            var pidIds = db.SetScanAsync(key);

            var transactionsFinished = new List<Task>();

            await foreach (var pidId in pidIds.WithCancellation(ct))
            {
                var pidKey = IdKeyFromPidId(pidId);
                if (pidKey == NoKey) continue;

                var transaction = db.CreateTransaction();
                transaction.AddCondition(Condition.HashEqual(pidKey, UniqueIdentity, pidId));
                _ = transaction.KeyDeleteAsync(pidKey);
                transactionsFinished.Add(transaction.ExecuteAsync());
            }

            transactionsFinished.Add(db.KeyDeleteAsync(key));

            await Task.WhenAll(transactionsFinished);
        }

        public async Task<StoredActivation?> TryGetExistingActivation(ClusterIdentity clusterIdentity,
            CancellationToken ct)
        {
            var activationStatus = await LookupKey(GetDb(), IdKey(clusterIdentity));
            return activationStatus?.Activation;
        }

        public void Dispose() => _connections.Dispose();

        public Task Init() => Task.CompletedTask;

        private IDatabase GetDb() => _connections.GetDatabase();

        private Task<bool> TryAcquireLockAsync(
            ClusterIdentity clusterIdentity,
            string requestId)
        {
            var key = IdKey(clusterIdentity);

            return GetDb().HashSetAsync(key, LockId, requestId, When.NotExists, CommandFlags.DemandMaster);
        }

        private static async Task<ActivationStatus?> LookupKey(IDatabaseAsync db, RedisKey key)
        {
            var result = await db.HashGetAllAsync(key);

            switch (result?.Length)
            {
                case 1:
                    return new ActivationStatus(result.First(entry => entry.Name == LockId).Value);
                case 4:
                    var values = result.ToDictionary();
                    return new ActivationStatus
                    (values[UniqueIdentity],
                        values[Address],
                        values[MemberId]
                    );
                default:
                    return null;
            }
        }

        private RedisKey IdKey(ClusterIdentity clusterIdentity) => IdKey(clusterIdentity.ToShortString());

        private RedisKey IdKeyFromPidId(string pidId)
            => IdentityStorageLookup.TryGetClusterIdentityShortString(pidId, out var clusterId)
                ? IdKey(clusterId!)
                : NoKey;

        private RedisKey IdKey(string clusterIdentity) => _clusterIdentityKey.Append(clusterIdentity);

        private RedisKey MemberKey(string memberId) => _memberKey.Append(memberId);

        private class ActivationStatus
        {
            public ActivationStatus(string? uniqueIdentity, string? address, string? memberId)
            {
                if (uniqueIdentity == null || address == null || memberId == null) throw new ArgumentException();

                Activation = new StoredActivation(memberId!, PID.FromAddress(address!, uniqueIdentity!));
            }

            public ActivationStatus(string? lockId)
            {
                ActiveLockId = lockId;
            }

            public StoredActivation? Activation { get; }

            public string? ActiveLockId { get; }
        }
    }
}