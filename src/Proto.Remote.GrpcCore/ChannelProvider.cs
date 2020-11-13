using Grpc.Core;

namespace Proto.Remote
{
    public class ChannelProvider : IChannelProvider
    {
        private readonly RemoteConfig _remoteConfig;

        public ChannelProvider(RemoteConfig remoteConfig)
        {
            _remoteConfig = remoteConfig;
        }

        public ChannelBase GetChannel(string address)
        {
            return new Channel(address, _remoteConfig.ChannelCredentials, _remoteConfig.ChannelOptions);
        }
    }
}