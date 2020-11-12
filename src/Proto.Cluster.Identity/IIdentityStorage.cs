namespace Proto.Cluster.Identity
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IIdentityStorage: IDisposable
    {
        public Task<StoredActivation?> TryGetExistingActivationAsync(ClusterIdentity clusterIdentity, CancellationToken token);

        public Task<SpawnLock?> TryAcquireLockAsync(ClusterIdentity clusterIdentity, CancellationToken ct);
        
        public Task<StoredActivation?> WaitForActivationAsync(ClusterIdentity clusterIdentity, CancellationToken token);

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct);

        public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken token);

        public Task RemoveActivation(PID pid, CancellationToken ct);

        public Task RemoveMemberIdAsync(string memberId, CancellationToken ct);
    }

    public class SpawnLock
    {
        public string LockId { get; }
        public ClusterIdentity ClusterIdentity { get; }

        public SpawnLock(string lockId, ClusterIdentity clusterIdentity)
        {
            LockId = lockId;
            ClusterIdentity = clusterIdentity;
        }
    }

    public class LookupResult
    {
        public StoredActivation? StoredActivation { get; }
        public SpawnLock? SpawnLock { get; }

        public LookupResult(StoredActivation storedActivation)
        {
            StoredActivation = storedActivation;
        }

        public LookupResult(SpawnLock spawnLock)
        {
            SpawnLock = spawnLock;
        }
    }

    public class StoredActivation
    {
        public StoredActivation(string memberId, PID pid)
        {
            MemberId = memberId;
            Pid = pid;
        }

        public PID Pid { get; }
        public string MemberId;
    }

    public class StorageFailure : Exception
    {
        public StorageFailure(string message) : base(message)
        {
        }

        public StorageFailure(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}