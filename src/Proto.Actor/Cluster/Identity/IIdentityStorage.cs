// -----------------------------------------------------------------------
// <copyright file="IIdentityStorage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Identity;

/// <summary>
///     The abstraction over identity storage used by <see cref="IdentityStorageLookup" />. Implement this interface to add
///     support for new databases.
/// </summary>
public interface IIdentityStorage : IDisposable
{
    /// <summary>
    ///     Retrieves the existing activation from the storage.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity to retrieve</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task<StoredActivation?> TryGetExistingActivation(
        ClusterIdentity clusterIdentity,
        CancellationToken ct
    );

    /// <summary>
    ///     Tries to acquire lock for specific cluster identity. The <see cref="IdentityStorageLookup" /> will lock specific
    ///     identity
    ///     to prevent multiple activations for the same identity happening at the same time.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity to lock</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns>The acquired lock</returns>
    public Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity, CancellationToken ct);

    /// <summary>
    ///     Used by the <see cref="IdentityStorageLookup" /> when it was not possible to acquire lock for specific identity.
    ///     This means that activation is in progress on another node and this method should wait until the lock is released
    ///     and return the activation. If the lock is determined to be stale, it should be removed and the method should return
    ///     null.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity to await for</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task<StoredActivation?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct);

    /// <summary>
    ///     Removes the lock
    /// </summary>
    /// <param name="spawnLock">Lock to remove</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task RemoveLock(SpawnLock spawnLock, CancellationToken ct);

    /// <summary>
    ///     Stores information about the virtual actor activation
    /// </summary>
    /// <param name="memberId">Member that activated the actor</param>
    /// <param name="spawnLock">
    ///     Lock acquired for the activation. If the lock id does not match the one currently
    ///     in the storage, this should throw <see cref="LockNotFoundException" />
    /// </param>
    /// <param name="pid">PID of the activation</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task StoreActivation(string memberId, SpawnLock spawnLock, PID pid, CancellationToken ct);

    /// <summary>
    ///     Removes activation from the storage.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity of the activation</param>
    /// <param name="pid">PID of the activation</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task RemoveActivation(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct);

    /// <summary>
    ///     Removes all activations for a specific member from the storage.
    /// </summary>
    /// <param name="memberId">Member id</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task RemoveMember(string memberId, CancellationToken ct);

    /// <summary>
    ///     Initialize the storage
    /// </summary>
    /// <returns></returns>
    public Task Init();
}

/// <summary>
///     Represents locked identity in the storage
/// </summary>
public class SpawnLock
{
    public SpawnLock(string lockId, ClusterIdentity clusterIdentity)
    {
        LockId = lockId;
        ClusterIdentity = clusterIdentity;
    }

    /// <summary>
    ///     Lock id
    /// </summary>
    public string LockId { get; }

    /// <summary>
    ///     Identity
    /// </summary>
    public ClusterIdentity ClusterIdentity { get; }
}

/// <summary>
///     Represents a virtual actor activation in the cluster.
/// </summary>
public class StoredActivation
{
    public StoredActivation(string memberId, PID pid)
    {
        MemberId = memberId;
        Pid = pid;
    }

    /// <summary>
    ///     PID of the virtual actor
    /// </summary>
    public PID Pid { get; }

    /// <summary>
    ///     Member hosting the virtual actor activation
    /// </summary>
    public string MemberId { get; }
}

#pragma warning disable RCS1194
public class StorageFailureException : Exception
#pragma warning restore RCS1194
{
    public StorageFailureException(string message) : base(message)
    {
    }

    public StorageFailureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

#pragma warning disable RCS1194
public class LockNotFoundException : StorageFailureException
#pragma warning restore RCS1194
{
    public LockNotFoundException(string message) : base(message)
    {
    }
}