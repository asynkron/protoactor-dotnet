// -----------------------------------------------------------------------
//  <copyright file="MiddlewareTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    class TestContextDecorator : ActorContextDecorator
    {
        private readonly List<string> _logs;

        public TestContextDecorator(IContext context, List<string> logs) : base(context)
        {
            _logs = logs;
        }

        public override Task Receive(MessageEnvelope envelope)
        {
            //only inspect "middleware" message
            if (envelope.Message is string str && (str == "middleware" || str == "decorator"))
            {
                _logs.Add("decorator");
                return base.Receive(envelope.WithMessage("decorator"));
            }

            return base.Receive(envelope);
        }
    }

    public class MiddlewareTests
    {
        private static readonly RootContext Context = new RootContext();


        [Fact]
        public void Given_ContextDecorator_Should_Call_Decorator_Before_Actor_Receive()
        {
            var logs = new List<string>();
            var logs2 = new List<string>();
            var logs3 = new List<string>();

            var testMailbox = new TestMailbox();
            var props = Props.FromFunc(c =>
                {
                    switch (c.Message)
                    {
                        //only inspect "decorator" message
                        case string str when str == "decorator":
                            logs.Add("actor");
                            return Actor.Done;
                        default:
                            return Actor.Done;
                    }
                })
                .WithMailbox(() => testMailbox)
                .WithContextDecorator(c => new TestContextDecorator(c, logs), c => new TestContextDecorator(c, logs2))
                .WithContextDecorator(c => new TestContextDecorator(c, logs3));
            var pid = Context.Spawn(props);

            Context.Send(pid, "middleware");

            Assert.Equal(2, logs.Count);
            Assert.Equal("decorator", logs[0]);
            Assert.Equal("actor", logs[1]);

            foreach (var log in new[] { logs2, logs3 })
            {
                Assert.Single(log);
                Assert.Equal("decorator", log[0]);
            }
        }


        [Fact]
        public void Given_ReceiveMiddleware_and_ContextDecorator_Should_Call_Middleware_and_Decorator_Before_Actor_Receive()
        {
            var logs = new List<string>();
            var testMailbox = new TestMailbox();
            var props = Props.FromFunc(c =>
                {
                    switch (c.Message)
                    {
                        //only inspect "decorator" message
                        case string str when str == "decorator":
                            logs.Add("actor");
                            return Actor.Done;
                        default:
                            return Actor.Done;
                    }
                })
                .WithReceiveMiddleware(
                    next => async (c, env) =>
                    {
                        //only inspect "start" message
                        if (env.Message is string str && str == "start")
                        {
                            logs.Add("middleware");
                            await next(c, env.WithMessage("middleware"));
                            return;
                        }

                        await next(c, env);
                    })
                .WithMailbox(() => testMailbox)
                .WithContextDecorator(c => new TestContextDecorator(c, logs));
            var pid = Context.Spawn(props);

            Context.Send(pid, "start");

            Console.WriteLine(string.Join(", ", logs));

            Assert.Equal(3, logs.Count);
            Assert.Equal("middleware", logs[0]);
            Assert.Equal("decorator", logs[1]);
            Assert.Equal("actor", logs[2]);
        }

        [Fact]
        public void Given_ReceiveMiddleware_Should_Call_Middleware_In_Order_Then_Actor_Receive()
        {
            var logs = new List<string>();
            var testMailbox = new TestMailbox();
            var props = Props.FromFunc(c =>
                {
                    if (c.Message is string)
                        logs.Add("actor");
                    return Actor.Done;
                })
                .WithReceiveMiddleware(
                    next => async (c, env) =>
                    {
                        if (env.Message is string)
                            logs.Add("middleware 1");
                        await next(c, env);
                    },
                    next => async (c, env) =>
                    {
                        if (env.Message is string)
                            logs.Add("middleware 2");
                        await next(c, env);
                    })
                .WithMailbox(() => testMailbox);
            var pid = Context.Spawn(props);

            Context.Send(pid, "");

            Assert.Equal(3, logs.Count);
            Assert.Equal("middleware 1", logs[0]);
            Assert.Equal("middleware 2", logs[1]);
            Assert.Equal("actor", logs[2]);
        }

        [Fact]
        public void Given_SenderMiddleware_Should_Call_Middleware_In_Order()
        {
            var logs = new List<string>();
            var pid1 = Context.Spawn(Props.FromProducer(() => new DoNothingActor()));
            var props = Props.FromFunc(c =>
                {
                    if (c.Message is string)
                        c.Send(pid1, "hey");
                    return Actor.Done;
                })
                .WithSenderMiddleware(
                    next => (c, t, e) =>
                    {
                        if (c.Message is string)
                            logs.Add("middleware 1");
                        return next(c, t, e);
                    },
                    next => (c, t, e) =>
                    {
                        if (c.Message is string)
                            logs.Add("middleware 2");
                        return next(c, t, e);
                    })
                .WithMailbox(() => new TestMailbox());
            var pid2 = Context.Spawn(props);

            Context.Send(pid2, "");

            Assert.Equal(2, logs.Count);
            Assert.Equal("middleware 1", logs[0]);
            Assert.Equal("middleware 2", logs[1]);
        }
    }
}