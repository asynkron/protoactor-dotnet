using Grpc.Core;
using Grpc.Net.Client;

namespace Proto.Remote.GrpcNet;

public class GrpcNetChannelProvider : IChannelProvider
{
    private readonly GrpcNetRemoteConfig _remoteConfig;

    public GrpcNetChannelProvider(GrpcNetRemoteConfig remoteConfig)
    {
        _remoteConfig = remoteConfig;
    }

    public ChannelBase GetChannel(string address)
    {
        var addressWithProtocol =
            $"{(_remoteConfig.UseHttps ? "https://" : "http://")}{address}";

        var channel = GrpcChannel.ForAddress(addressWithProtocol,
            _remoteConfig?.ChannelOptions ?? new GrpcChannelOptions());

        return channel;
    }
}