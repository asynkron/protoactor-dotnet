using System;
using System.Threading.Tasks;
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

        public class DoNothingActor : IActor
        {
            public Task ReceiveAsync(IContext context) => Actor.Done;
        }

        public class DoNothingSupervisorStrategy : ISupervisorStrategy
        {
            public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception cause) { }
        }

        public class TestMailbox : IMailbox
        {
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

        public class TestDispatcher : IDispatcher
        {
            public int Throughput => 10;

            public void Schedule(Func<Task> runner) => runner().Wait();
        }

      
    }
}