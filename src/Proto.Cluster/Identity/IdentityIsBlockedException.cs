// -----------------------------------------------------------------------
// <copyright file = "IdentityBlockedException.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Cluster.Identity;

/// <summary>
///     Lets the caller know that the identity is not available to spawn.
/// </summary>
#pragma warning disable RCS1194
public class IdentityIsBlockedException : Exception
#pragma warning restore RCS1194
{
    public IdentityIsBlockedException(ClusterIdentity blockedIdentity)
    {
        BlockedIdentity = blockedIdentity;
    }

    public ClusterIdentity BlockedIdentity { get; }
}