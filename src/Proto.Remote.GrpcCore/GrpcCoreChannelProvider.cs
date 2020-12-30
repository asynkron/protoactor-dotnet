using Grpc.Core;

namespace Proto.Remote.GrpcCore
{
    public class GrpcCoreChannelProvider : IChannelProvider
    {
        private readonly GrpcCoreRemoteConfig _remoteConfig;

        public GrpcCoreChannelProvider(GrpcCoreRemoteConfig remoteConfig) => _remoteConfig = remoteConfig;

        public ChannelBase GetChannel(string address) => new Channel(address, _remoteConfig.ChannelCredentials, _remoteConfig.ChannelOptions);
    }
}