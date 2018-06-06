namespace Proto
{
    public interface IMessageContext
    {
        object Message { get; }

        MessageHeader Headers { get; }
    }
}