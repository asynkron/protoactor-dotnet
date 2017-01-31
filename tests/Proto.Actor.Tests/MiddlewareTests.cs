using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Xunit;

namespace Proto.Tests
{
    public class MiddlewareTests
    {
        [Fact]
        public void Given_Middleware_Should_Call_In_Order_Then_Actor()
        {
            var logs = new List<int>();
            var testMailbox = new ActorFixture.TestMailbox();
            var actor = Actor.FromFunc(c =>
            {
                switch(c.Message)
                {
                    case string s:
                        logs.Add(2);
                        break;
                }
                return Actor.Done;
            })
            .WithMiddleware(
                next => async c =>
                {
                    switch (c.Message)
                    {
                        case string s:
                            logs.Add(0);
                            break;
                    }
                    await next(c);
                },
                next => async c =>
                {
                    switch (c.Message)
                    {
                        case string s:
                            logs.Add(1);
                            break;
                    }
                    await next(c);
                })
            .WithMailbox(() => testMailbox);
            var pid = Actor.Spawn(actor);

            pid.Tell("");

            Assert.Equal(3, logs.Count);
            Assert.Equal(0, logs[0]);
            Assert.Equal(1, logs[1]);
            Assert.Equal(2, logs[2]);
        }
    }

    static class ActorFixture
    {
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
