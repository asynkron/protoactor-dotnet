// -----------------------------------------------------------------------
// <copyright file="RedisIdentityStorage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;
using StackExchange.Redis;

namespace Proto.Cluster.Identity.Redis
{
    public class RedisIdentityStorage : IIdentityStorage
    {
        private static readonly ILogger Logger = Log.CreateLogger<RedisIdentityStorage>();

        private static readonly RedisValue UniqueIdentity = "pid";
        private static readonly RedisValue Address = "adr";
        private static readonly RedisValue MemberId = "mid";
        private static readonly RedisValue LockId = "lid";
        private readonly AsyncSemaphore _asyncSemaphore;

        private readonly RedisKey _clusterIdentityKey;

        private readonly IConnectionMultiplexer _connections;
        private readonly TimeSpan _maxLockTime;
        private readonly RedisKey _memberKey;

        public RedisIdentityStorage(
            string clusterName,
            IConnectionMultiplexer connections,
            TimeSpan? maxWaitBeforeStaleLock = null,
            int maxConcurrency = 200
        )
        {
            RedisKey baseKey = clusterName + ":";
            _clusterIdentityKey = baseKey.Append("ci:");
            _memberKey = baseKey.Append("mb:");
            _connections = connections;
            _maxLockTime = maxWaitBeforeStaleLock ?? TimeSpan.FromSeconds(5);
            _asyncSemaphore = new AsyncSemaphore(maxConcurrency);
        }

        public async Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var requestId = Guid.NewGuid().ToString("N");
            var hasLock = await _asyncSemaphore.WaitAsync(() => TryAcquireLockAsync(clusterIdentity, requestId)).ConfigureAwait(false);

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

            var activationStatus = await LookupKey(db, key).ConfigureAwait(false);
            var lockId = activationStatus?.ActiveLockId;

            if (lockId != null)
            {
                //There is an active lock on the pid, spin wait
                var i = 1;

                do await Task.Delay(20 * i++, ct);
                while (!ct.IsCancellationRequested
                    && _maxLockTime > timer.Elapsed
                    && (activationStatus = await LookupKey(db, key).ConfigureAwait(false))?.ActiveLockId == lockId
                );
            }

            //the lookup entity was lost, stale lock maybe?
            if (activationStatus == null) return null;

            //lookup was unlocked, return this pid
            if (activationStatus.Activation != null)
                return activationStatus.Activation;

            //Still locked but not by the same request that originally locked it, so not stale
            if (activationStatus.ActiveLockId != lockId) return null;

            //Stale lock. just delete it and let cluster retry
            await RemoveLock(new SpawnLock(lockId!, clusterIdentity), CancellationToken.None).ConfigureAwait(false);

            return null;
        }

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct)
        {
            var db = GetDb();

            var key = IdKey(spawnLock.ClusterIdentity);
            var transaction = db.CreateTransaction();
            transaction.AddCondition(Condition.HashEqual(key, LockId, spawnLock.LockId));
            _ = transaction.HashDeleteAsync(key, LockId);
            return transaction.ExecuteAsync();
        }

        public async Task StoreActivation(
            string memberId,
            SpawnLock spawnLock,
            PID pid,
            CancellationToken ct
        )
        {
            var key = IdKey(spawnLock.ClusterIdentity);

            var values = new[]
            {
                new HashEntry(UniqueIdentity, pid.Id),
                new HashEntry(Address, pid.Address),
                new HashEntry(MemberId, memberId),
                new HashEntry(LockId, RedisValue.EmptyString)
            };

            var executed = await _asyncSemaphore.WaitAsync(() => {
                    var db = GetDb();

                    var transaction = db.CreateTransaction();
                    transaction.AddCondition(Condition.HashEqual(key, LockId, spawnLock.LockId));
                    _ = transaction.HashSetAsync(key, values, CommandFlags.DemandMaster);
                    _ = transaction.SetAddAsync(MemberKey(memberId), key.ToString());
                    _ = transaction.KeyPersistAsync(key);
                    return transaction.ExecuteAsync();
                }
            ).ConfigureAwait(false);

            if (!executed) throw new LockNotFoundException($"Failed to store activation of {pid}");
        }

        public Task RemoveActivation(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            Logger.LogDebug("Removing activation: {ClusterIdentity} {@PID}", clusterIdentity, pid);

            const string removePid = "local pidEntry = redis.call('HMGET', KEYS[1], 'pid', 'adr', 'mid');\n" +
                                     "if pidEntry[1]~=ARGV[1] or pidEntry[2]~=ARGV[2] then return 0 end;\n" + // id / address matches
                                     "local memberKey = ARGV[3] .. pidEntry[3];\n" +
                                     "redis.call('SREM', memberKey, KEYS[1] .. '');" +
                                     "return redis.call('DEL', KEYS[1]);";

            var key = IdKey(clusterIdentity);
            return _asyncSemaphore.WaitAsync(()
                    => {
                    return GetDb().ScriptEvaluateAsync(removePid, new[] {key}, new RedisValue[] {pid.Id, pid.Address, _memberKey.ToString()}
                    );
                }
            );
        }

        public Task RemoveMember(string memberId, CancellationToken ct)
        {
            var memberKey = MemberKey(memberId);
            RedisValue mVal = memberKey.ToString();

            const string removeMember = "local cursor = 0\n" +
                                        "repeat\n" +
                                        " local rep = redis.call('SSCAN', ARGV[1], cursor)\n" +
                                        " if rep[2][1] == nil then break end" +
                                        " cursor = rep[1]\n" +
                                        " redis.call('DEL', unpack(rep[2]))\n" +
                                        "until cursor == '0'\n" +
                                        "redis.call('DEL', KEYS[1]);";

            return _asyncSemaphore.WaitAsync(() => GetDb().ScriptEvaluateAsync(removeMember, new[] {memberKey}, new[] {mVal}));
        }

        public async Task<StoredActivation?> TryGetExistingActivation(
            ClusterIdentity clusterIdentity,
            CancellationToken ct
        )
        {
            var activationStatus = await LookupKey(GetDb(), IdKey(clusterIdentity)).ConfigureAwait(false);
            return activationStatus?.Activation;
        }

        public void Dispose()
        {
        }

        public Task Init() => Task.CompletedTask;

        private IDatabase GetDb() => _connections.GetDatabase();

        private Task<bool> TryAcquireLockAsync(
            ClusterIdentity clusterIdentity,
            string requestId
        )
        {
            var key = IdKey(clusterIdentity);

            var db = GetDb();
            var transaction = db.CreateTransaction();

            transaction.AddCondition(Condition.KeyNotExists(key));
            transaction.HashSetAsync(key, LockId, requestId);
            transaction.KeyExpireAsync(key, _maxLockTime);

            return transaction.ExecuteAsync();
        }

        private async Task<ActivationStatus?> LookupKey(IDatabaseAsync db, RedisKey key)
        {
            var result = await _asyncSemaphore.WaitAsync(() => db.HashGetAllAsync(key)).ConfigureAwait(false);

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

        private RedisKey IdKey(ClusterIdentity clusterIdentity) => _clusterIdentityKey.Append(clusterIdentity.ToString());

        private RedisKey MemberKey(string memberId) => _memberKey.Append(memberId);

        private class ActivationStatus
        {
            public ActivationStatus(string? uniqueIdentity, string? address, string? memberId)
            {
                if (uniqueIdentity == null || address == null || memberId == null) throw new ArgumentException();

                Activation = new StoredActivation(memberId!, PID.FromAddress(address!, uniqueIdentity!));
            }

            public ActivationStatus(string? lockId) => ActiveLockId = lockId;

            public StoredActivation? Activation { get; }

            public string? ActiveLockId { get; }
        }
    }
}