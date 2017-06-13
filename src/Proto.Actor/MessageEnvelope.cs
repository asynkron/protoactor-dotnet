namespace Proto
{
    public class MessageEnvelope
    {
        public MessageEnvelope(object message, PID sender, MessageHeader header)
        {
            Sender = sender; // ?? throw new ArgumentNullException(nameof(sender));
            Message = message; // ?? throw new ArgumentNullException(nameof(message));
            Header = header;
        }

        public PID Sender { get; }
        public object Message { get; }
        public MessageHeader Header { get; private set; }


        public static (object message, PID sender, MessageHeader headers) Unwrap(object message)
        {
            if (message is MessageEnvelope envelope)
            {
                return (envelope.Message, envelope.Sender, envelope.Header);
            }

            return (message, null, null);
        }

        public string GetHeader(string key, string @default = null)
        {
            if (Header == null)
            {
                return @default;
            }
            return Header.TryGetValue(key, out string value) ? value : @default;
        }

        public void SetHeader(string key, string value)
        {
            if (Header == null)
            {
                Header = new MessageHeader();
            }

            Header[key] = value;
        }
    }
}