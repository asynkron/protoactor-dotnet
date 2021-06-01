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
    public sealed class IdentityStorageLogging : IIdentityStorage
    {
        private readonly ILogger _logger = Log.CreateLogger<IdentityStorageLogging>();
        private readonly IIdentityStorage _storage;

        public IdentityStorageLogging(IIdentityStorage storage) => _storage = storage;

        public Task<StoredActivation?> TryGetExistingActivation(
            ClusterIdentity clusterIdentity,
            CancellationToken ct
        ) => LogCall(() => _storage.TryGetExistingActivation(clusterIdentity, ct),
            nameof(TryGetExistingActivation), clusterIdentity.ToString()
        );

        public Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct) =>
            LogCall(() => _storage.TryAcquireLock(clusterIdentity, ct),
                nameof(TryAcquireLock), clusterIdentity.ToString()
            );

        public Task<StoredActivation?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct) =>
            LogCall(() => _storage.WaitForActivation(clusterIdentity, ct),
                nameof(WaitForActivation), clusterIdentity.ToString()
            );

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct) =>
            LogCall(() => _storage.RemoveLock(spawnLock, ct),
                nameof(RemoveLock), spawnLock.ClusterIdentity.ToString()
            );

        public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken ct) =>
            LogCall(() => _storage.StoreActivation(memberId, spawnLock, pid, ct),
                nameof(StoreActivation), spawnLock.ClusterIdentity.ToString()
            );

        public Task RemoveActivation(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct) => LogCall(
            () => _storage.RemoveActivation(clusterIdentity, pid, ct),
            nameof(RemoveActivation), pid.ToString()
        );

        public Task RemoveMember(string memberId, CancellationToken ct) => LogCall(() => _storage.RemoveMember(memberId, ct),
            nameof(RemoveMember), memberId
        );

        public Task Init() => LogCall(() => _storage.Init(),
            nameof(Init), ""
        );

        public void Dispose() => _storage.Dispose();

        private async Task LogCall(Func<Task> call, string method, string subject)
        {
            var timer = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("{Method}: {Subject} before",
                    method, subject, timer.Elapsed
                );
                await call();
                timer.Stop();
                _logger.LogInformation("{Method}: {Subject} after {Elapsed}",
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
                _logger.LogInformation("{Method}: {Subject} before",
                    method, subject
                );
                var result = await call();
                timer.Stop();
                _logger.LogInformation("{Method}: {Subject} after {Elapsed} returned {Result}",
                    method, subject, timer.Elapsed, result
                );
                return result;
            }
            catch (Exception e)
            {
                timer.Stop();
                _logger.LogError(e, "{Method}: {Subject} failed after {Elapsed}: {Error}",
                    method, subject, timer.Elapsed, e.ToString()
                );
                throw;
            }
        }
    }
}