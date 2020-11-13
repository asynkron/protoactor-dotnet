using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;

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
            var addressWithProtocol =
                $"{(_remoteConfig.UseHttps ? "https://" : "http://")}{address}";

            var channel = GrpcChannel.ForAddress(addressWithProtocol, _remoteConfig?.ChannelOptions ?? new GrpcChannelOptions());
            return channel;
        }
    }
}