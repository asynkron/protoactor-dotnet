// -----------------------------------------------------------------------
//   <copyright file="ServerEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Remote
{
    /// <summary>
    /// Handles a connection to a remote endpoint.
    /// </summary>
    public class ServerEndpoint : Endpoint
    {
        public ServerEndpoint(ActorSystem system, RemoteConfigBase remoteConfig, string address, IChannelProvider channelProvider, ServerConnector.Type type, RemoteMessageHandler remoteMessageHandler) : base(address, system, remoteConfig)
            => Connector = new ServerConnector(Address, type, this, channelProvider, System, RemoteConfig, remoteMessageHandler);

        public ServerConnector Connector { get; }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await Connector.Stop().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}