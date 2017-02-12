// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;

namespace Proto.Tests
{
    static class ActorFixture
    {
        public static Receive EmptyReceive = c => Actor.Done;

        public class TestMailbox : IMailbox
        {
            private IDispatcher _dispatcher;
            private IMessageInvoker _invoker;

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