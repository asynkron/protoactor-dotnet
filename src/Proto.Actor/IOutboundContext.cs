namespace Proto
{
    public interface ISenderContext    {
        object Message { get; }

        MessageHeader Headers { get; }
    }
}