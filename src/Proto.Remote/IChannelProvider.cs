using Grpc.Core;

namespace Proto.Remote
{
    public interface IChannelProvider
    {
        ChannelBase GetChannel(string address);
    }
}