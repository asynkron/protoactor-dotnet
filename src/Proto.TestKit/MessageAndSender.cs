namespace Proto.TestKit
{
    class MessageAndSender
    {
        public object? Message { get; }
        public PID? Sender { get; }

        public MessageAndSender(ISenderContext context) : this(context.Sender, context.Message) { }

        private MessageAndSender(PID? sender, object? message)
        {
            Sender = sender;
            Message = message;
        }
    }
}
