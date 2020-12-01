using System.Collections.Generic;
using Grpc.Core;
using JetBrains.Annotations;

namespace Proto.Remote.GrpcCore
{
    [PublicAPI]
    public record GrpcCoreRemoteConfig : RemoteConfigBase
    {
        protected GrpcCoreRemoteConfig(string host, int port) : base(host, port)
        {
        }

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

        public static GrpcCoreRemoteConfig BindToAllInterfaces(string advertisedHost, int port = 0) =>
            new GrpcCoreRemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);

        public static GrpcCoreRemoteConfig BindToLocalhost(int port = 0) => new(Localhost, port);

        public static GrpcCoreRemoteConfig BindTo(string host, int port = 0) => new(host, port);
    }
}