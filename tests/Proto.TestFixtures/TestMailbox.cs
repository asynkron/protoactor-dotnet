// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestMailbox : IMailbox
    {
        private IMessageInvoker _invoker;
        public List<object> UserMessages { get; } = new List<object>();
        public List<object> SystemMessages { get; } = new List<object>();
        
        public Task PostUserMessage(object msg)
        {
            UserMessages.Add(msg);
            return _invoker?.InvokeUserMessageAsync(msg);
        }

        public Task PostSystemMessage(object msg)
        {
            SystemMessages.Add(msg);
            return _invoker?.InvokeSystemMessageAsync(msg);
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