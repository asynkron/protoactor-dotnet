// -----------------------------------------------------------------------
// <copyright file="IdentityStorageLogging.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Identity
{
    public class IdentityStorageLogging : IIdentityStorage
    {
        private readonly ILogger _logger = Log.CreateLogger<IdentityStorageLogging>();
        private readonly IIdentityStorage _storage;

        public IdentityStorageLogging(IIdentityStorage storage)
        {
            _storage = storage;
        }

        public Task<StoredActivation?> TryGetExistingActivation(ClusterIdentity clusterIdentity,
            CancellationToken ct)
        {
            return LogCall(() => _storage.TryGetExistingActivation(clusterIdentity, ct),
                nameof(TryGetExistingActivation), clusterIdentity.ToShortString()
            );
        }

        public Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            return LogCall(() => _storage.TryAcquireLock(clusterIdentity, ct),
                nameof(TryAcquireLock), clusterIdentity.ToShortString()
            );
        }

        public Task<StoredActivation?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            return LogCall(() => _storage.WaitForActivation(clusterIdentity, ct),
                nameof(WaitForActivation), clusterIdentity.ToShortString()
            );
        }

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct)
        {
            return LogCall(() => _storage.RemoveLock(spawnLock, ct),
                nameof(RemoveLock), spawnLock.ClusterIdentity.ToShortString()
            );
        }

        public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken ct)
        {
            return LogCall(() => _storage.StoreActivation(memberId, spawnLock, pid, ct),
                nameof(StoreActivation), spawnLock.ClusterIdentity.ToShortString()
            );
        }

        public Task RemoveActivation(PID pid, CancellationToken ct)
        {
            return LogCall(() => _storage.RemoveActivation(pid, ct),
                nameof(RemoveActivation), pid.ToShortString()
            );
        }

        public Task RemoveMember(string memberId, CancellationToken ct)
        {
            return LogCall(() => _storage.RemoveMember(memberId, ct),
                nameof(RemoveMember), memberId
            );
        }

        public Task Init()
        {
            return LogCall(() => _storage.Init(),
                nameof(Init), ""
            );
        }

        public void Dispose()
        {
            _storage.Dispose();
        }

        private async Task LogCall(Func<Task> call, string method, string subject)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                await call();
                timer.Stop();
                _logger.LogDebug("{Method}: {Subject} after {Elapsed}",
                    method, subject, timer.Elapsed
                );
            }
            catch (Exception e)
            {
                timer.Stop();
                _logger.LogError(e, "{Method}: {Subject} failed after {Elapsed}",
                    method, subject, timer.Elapsed
                );
                throw;
            }
        }

        private async Task<T> LogCall<T>(Func<Task<T>> call, string method, string subject)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var result = await call();
                timer.Stop();
                _logger.LogDebug("{Method}: {Subject} after {Elapsed}",
                    method, subject, timer.Elapsed
                );
                return result;
            }
            catch (Exception e)
            {
                timer.Stop();
                _logger.LogError(e, "{Method}: {Subject} failed after {Elapsed}",
                    method, subject, timer.Elapsed
                );
                throw;
            }
        }
    }
}