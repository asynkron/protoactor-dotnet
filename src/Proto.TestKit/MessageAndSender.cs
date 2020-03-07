namespace Proto.TestKit
{
    internal class MessageAndSender
    {
        public object Message { get; set; }
        public PID Sender { get; set; }

        public MessageAndSender(ISenderContext context) : this(context.Sender, context.Message)
        {
        }

        public MessageAndSender(PID sender, object message)
        {
            Sender = sender;
            Message = message;
        }
    }
}