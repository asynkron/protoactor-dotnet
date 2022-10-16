// -----------------------------------------------------------------------
// <copyright file="IClusterProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Diagnostics;

namespace Proto.Cluster;

/// <summary>
///     Cluster provider is responsible for determining the members of the cluster and what cluster kinds they support.
///     The cluster provider updates the <see cref="MemberList" />
/// </summary>
[PublicAPI]
public interface IClusterProvider : IDiagnosticsProvider
{
    /// <summary>
    ///     Starts the cluster provider
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    Task StartMemberAsync(Cluster cluster);

    /// <summary>
    ///     Starts the cluster provider in client mode. The client member does not host any virtual actors and it is not
    ///     registered in the membership provider.
    ///     It only monitors other member's presence and allows to send messages to virtual actors hosted by other members.
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    Task StartClientAsync(Cluster cluster);

    /// <summary>
    ///     Shuts down the cluster provider
    /// </summary>
    /// <param name="graceful"></param>
    /// <returns></returns>
    Task ShutdownAsync(bool graceful);
}