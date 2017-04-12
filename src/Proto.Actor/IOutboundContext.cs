namespace Proto
{
    public interface IOutboundContext
    {
        object Message { get; }

        MessageHeader MessageHeader { get; }
    }
}