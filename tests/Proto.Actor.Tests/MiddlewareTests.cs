// -----------------------------------------------------------------------
//  <copyright file="MiddlewareTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class MiddlewareTests
    {
        [Fact]
        public void Given_ReceiveMiddleware_Should_Call_Middleware_In_Order_Then_Actor_Receive()
        {
            var logs = new List<string>();
            var testMailbox = new TestMailbox();
            var props = Actor.FromFunc(c =>
                {
                    if (c.Message is string)
                        logs.Add("actor");
                    return Actor.Done;
                })
                .WithReceiveMiddleware(
                    next => async c =>
                    {
                        if (c.Message is string)
                            logs.Add("middleware 1");
                        await next(c);
                    },
                    next => async c =>
                    {
                        if (c.Message is string)
                            logs.Add("middleware 2");
                        await next(c);
                    })
                .WithMailbox(() => testMailbox);
            var pid = Actor.Spawn(props);

            pid.Tell("");

            Assert.Equal(3, logs.Count);
            Assert.Equal("middleware 1", logs[0]);
            Assert.Equal("middleware 2", logs[1]);
            Assert.Equal("actor", logs[2]);
        }

        [Fact]
        public void Given_SenderMiddleware_Should_Call_Middleware_In_Order()
        {
            var logs = new List<string>();
            var pid1 = Actor.Spawn(Actor.FromProducer(() => new DoNothingActor()));
            var props = Actor.FromFunc(c =>
                {
                    if (c.Message is string)
                        c.Tell(pid1, "hey");
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
            var pid2 = Actor.Spawn(props);

            pid2.Tell("");

            Assert.Equal(2, logs.Count);
            Assert.Equal("middleware 1", logs[0]);
            Assert.Equal("middleware 2", logs[1]);
        }
    }
}