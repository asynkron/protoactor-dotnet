using Proto.Mailbox;

namespace Proto.Tests
{
    static class ActorFixture
    {
        public static Receive EmptyReceive = c => Actor.Done;

        public class TestMailbox : IMailbox
        {
            private IMessageInvoker _invoker;
            private IDispatcher _dispatcher;

            public void PostUserMessage(object msg)
            {
                _invoker.InvokeUserMessageAsync(msg).Wait();
            }

            public void PostSystemMessage(object msg)
            {
                _invoker.InvokeSystemMessageAsync(msg);
            }

            public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
            {
                _invoker = invoker;
            }

            public void Start()
            {
            }
        }
    }
}