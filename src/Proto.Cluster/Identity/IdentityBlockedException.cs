﻿// -----------------------------------------------------------------------
// <copyright file = "IdentityBlockedException.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Identity;

/// <summary>
/// Lets the caller know that the identity is not available to spawn.
/// </summary>
public class IdentityBlockedException : Exception
{
    public IdentityBlockedException(ClusterIdentity blockedIdentity) => BlockedIdentity = blockedIdentity;

    public ClusterIdentity BlockedIdentity { get; }
}