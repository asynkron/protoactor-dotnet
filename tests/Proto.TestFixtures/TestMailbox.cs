// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestMailbox : IMailbox
    {
        private IMessageInvoker _invoker;
        public List<object> UserMessages { get; } = new List<object>();
        public List<object> SystemMessages { get; } = new List<object>();
        
        public void PostUserMessage(object msg)
        {
            UserMessages.Add(msg);
            _invoker?.InvokeUserMessageAsync(msg).Wait();
        }

        public void PostSystemMessage(object msg)
        {
            if (msg is Stop)
                _invoker.CancellationTokenSource.Cancel();
            SystemMessages.Add(msg);
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