// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;

namespace Proto.Tests.Fixture
{
    public class TestMailbox : IMailbox
    {
        private IMessageInvoker _invoker;

        public void PostUserMessage(object msg)
        {
            _invoker?.InvokeUserMessageAsync(msg).Wait();
        }

        public void PostSystemMessage(object msg)
        {
            _invoker?.InvokeSystemMessageAsync(msg).Wait();
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