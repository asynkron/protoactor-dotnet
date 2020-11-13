using System;
using System.Collections.Generic;
using Grpc.Core;

namespace Proto.Remote
{
    public record RemoteConfig : RemoteConfigBase
    {
        protected RemoteConfig(string host, int port) : base(host, port)
        {
        }
        
        public static RemoteConfig BinToAllInterfaces(string advertisedHost, int port = 0) =>
            new RemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);
        
        public static RemoteConfig BindToLocalhost(int port = 0) => new RemoteConfig(Localhost, port);
        
        public static RemoteConfig BindTo(string host, int port = 0) => new RemoteConfig(host, port);
        /// <summary>
        ///     Gets or sets the ChannelOptions for the gRPC channel.
        /// </summary>
        public IEnumerable<ChannelOption> ChannelOptions { get; init; } = null!;

        /// <summary>
        ///     Gets or sets the ChannelCredentials for the gRPC channel. The default is Insecure.
        /// </summary>
        public ChannelCredentials ChannelCredentials { get; init; } = ChannelCredentials.Insecure;

        /// <summary>
        ///     Gets or sets the ServerCredentials for the gRPC server. The default is Insecure.
        /// </summary>
        public ServerCredentials ServerCredentials { get; init; } = ServerCredentials.Insecure;
    }
}