// -----------------------------------------------------------------------
//   <copyright file="ServerEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Remote.GrpcNet;

namespace Proto.Remote;

/// <summary>
///     Handles a connection to a remote endpoint.
/// </summary>
    public sealed class ServerEndpoint : Endpoint
    {
        public ServerEndpoint(ActorSystem system, GrpcNetRemoteConfig remoteConfig, string remoteAddress,
            ServerConnector.Type type, RemoteMessageHandler remoteMessageHandler) : base(remoteAddress, system, remoteConfig)
        {
            Connector = new ServerConnector(RemoteAddress, type, this, System, remoteConfig, remoteMessageHandler);
        }

        public ServerConnector Connector { get; }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        await Connector.Stop().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}