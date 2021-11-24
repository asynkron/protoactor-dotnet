// -----------------------------------------------------------------------
//   <copyright file="ServerSideClientEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    /// <summary>
    /// Handles connection to a client actor system.
    /// </summary>
    public class ServerSideClientEndpoint : Endpoint
    {
        public ServerSideClientEndpoint(ActorSystem system, RemoteConfigBase remoteConfig, string address) : base(address, system, remoteConfig) { }
    }
}