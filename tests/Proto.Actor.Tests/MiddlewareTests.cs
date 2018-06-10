// -----------------------------------------------------------------------
//  <copyright file="MiddlewareTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class MiddlewareTests
    {
        private static readonly RootContext Context = new RootContext();
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
                    next => async (c,env) =>
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

            Context.Send(pid,"");

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