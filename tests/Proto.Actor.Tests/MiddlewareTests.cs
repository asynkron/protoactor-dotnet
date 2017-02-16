// -----------------------------------------------------------------------
//  <copyright file="MiddlewareTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Xunit;

namespace Proto.Tests
{
    public class MiddlewareTests
    {
        [Fact]
        public void Given_Middleware_Should_Call_In_Order_Then_Actor()
        {
            var logs = new List<string>();
            var testMailbox = new Fixture.TestMailbox();
            var props = Actor.FromFunc(c =>
                {
                    if (c.Message is string)
                    {
                        logs.Add("actor");
                    }
                    return Actor.Done;
                })
                .WithMiddleware(
                    next => async c =>
                    {
                        if (c.Message is string)
                        {
                            logs.Add("middleware 1");
                        }
                        await next(c);
                    },
                    next => async c =>
                    {
                        if (c.Message is string)
                        {
                            logs.Add("middleware 2");
                        }
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
    }
}