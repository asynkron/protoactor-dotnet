using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class ReceivePluginTests
    {
        [Fact]
        public void Given_ReceivePlugins_Should_Call_In_Order_Then_Actor()
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
            .WithReceivers(
                async c =>
                {
                    switch (c.Message)
                    {
                        case string s:
                            logs.Add(0);
                            break;
                    }
                    await c.NextAsync();
                },
                async c =>
                {
                    switch (c.Message)
                    {
                        case string s:
                            logs.Add(1);
                            break;
                    }
                    await c.NextAsync();
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

            public void PostSystemMessage(SystemMessage sys)
            {
                _invoker.InvokeSystemMessageAsync(sys);
            }

            public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
            {
                _invoker = invoker;
            }
        }
    }
}
