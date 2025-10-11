// -----------------------------------------------------------------------
//   <copyright file="ClientRemoteEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote;

/// <summary>
///     Handles connection to a client actor system.
/// </summary>
public sealed class ClientRemoteEndpoint : RemoteEndpointBase
{
    public ClientRemoteEndpoint(ActorSystem system, RemoteConfig remoteConfig, string remoteAddress) : base(
        remoteAddress, system, remoteConfig)
    {
    }
}