// -----------------------------------------------------------------------
// <copyright file="IIdentityStorage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Identity
{
    public interface IIdentityStorage : IDisposable
    {
        public Task<StoredActivation?> TryGetExistingActivation(ClusterIdentity clusterIdentity,
            CancellationToken ct);

        public Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct);

        /// <summary>
        ///     Wait on lock, return activation when present. Responsible for deleting stale locks
        /// </summary>
        /// <param name="clusterIdentity"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<StoredActivation?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct);

        public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct);

        public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken ct);

        public Task RemoveActivation(PID pid, CancellationToken ct);

        public Task RemoveMember(string memberId, CancellationToken ct);

        public Task Init();
    }

    public class SpawnLock
    {
        public SpawnLock(string lockId, ClusterIdentity clusterIdentity)
        {
            LockId = lockId;
            ClusterIdentity = clusterIdentity;
        }

        public string LockId { get; }
        public ClusterIdentity ClusterIdentity { get; }
    }

    public class StoredActivation
    {
        public StoredActivation(string memberId, PID pid)
        {
            MemberId = memberId;
            Pid = pid;
        }

        public PID Pid { get; }
        public string MemberId { get; }
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

    public class LockNotFoundException : StorageFailure
    {
        public LockNotFoundException(string message) : base(message)
        {
        }
    }
}