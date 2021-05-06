#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Identity.Tests
{
    public sealed class FailureInjectionStorage : IIdentityStorage
    {
        private const double SuccessRate = 0.8;
        private static readonly Random Mayhem = new();
        private readonly IIdentityStorage _identityStorageImplementation;

        public FailureInjectionStorage(IIdentityStorage identityStorageImplementation)
            => _identityStorageImplementation = identityStorageImplementation;

        public void Dispose() => _identityStorageImplementation.Dispose();

        public Task<StoredActivation?> TryGetExistingActivation(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            MaybeFail();
            return _identityStorageImplementation.TryGetExistingActivation(clusterIdentity, ct);
        }

        public Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            MaybeFail();
            return _identityStorageImplementation.TryAcquireLock(clusterIdentity, ct);
        }

        public Task<StoredActivation?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            MaybeFail();
            return _identityStorageImplementation.WaitForActivation(clusterIdentity, ct);
        }

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct)
        {
            MaybeFail();
            return _identityStorageImplementation.RemoveLock(spawnLock, ct);
        }

        public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken ct)
        {
            if (Mayhem.NextDouble() > SuccessRate)
            {
                RemoveLock(spawnLock, ct);
                throw Mayhem.Next() % 2 == 0
                    ? new Exception("Activation fail")
                    : new LockNotFoundException("fake lock");
            }

            return _identityStorageImplementation.StoreActivation(memberId, spawnLock, pid, ct);
        }

        public Task RemoveActivation(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct) =>
            // MaybeFail();
            _identityStorageImplementation.RemoveActivation(clusterIdentity, pid, ct);

        public Task RemoveMember(string memberId, CancellationToken ct) => _identityStorageImplementation.RemoveMember(memberId, ct);

        public Task Init() => _identityStorageImplementation.Init();

        private static void MaybeFail()
        {
            if (Mayhem.NextDouble() > SuccessRate) throw new Exception("Chaos monkey at work");
        }
    }
}