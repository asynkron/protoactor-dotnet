// -----------------------------------------------------------------------
//   <copyright file="ServerSideClientEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote;

/// <summary>
///     Handles connection to a client actor system.
/// </summary>
public sealed class ServerSideClientEndpoint : Endpoint
{
    public ServerSideClientEndpoint(ActorSystem system, RemoteConfigBase remoteConfig, string remoteAddress) : base(
        remoteAddress, system, remoteConfig)
    {
    }
}