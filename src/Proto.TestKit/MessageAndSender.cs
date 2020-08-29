namespace Proto.TestKit
{
    internal class MessageAndSender
    {
        public MessageAndSender(ISenderContext context) : this(context.Sender, context.Message)
        {
        }

        private MessageAndSender(PID? sender, object? message)
        {
            Sender = sender;
            Message = message;
        }

        public object? Message { get; }
        public PID? Sender { get; }
    }
}