// -----------------------------------------------------------------------
// <copyright file="MessageHeaderTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests.Headers
{
    public class MessageHeaderTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MessageHeaderTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

        [Fact]
        public async Task HeadersArePropagatedBackInReply()
        {
            Sender PropagateHeaders(Sender next) =>
                (context, target, envelope) =>
                    next(context, target, envelope.WithHeader(context.Headers));

            ActorSystem system = new ActorSystem();
            Props props1 = Props.FromFunc(ctx =>
                {
                    switch (ctx.Message)
                    {
                        case SomeRequest:
                            ctx.Respond(new SomeResponse());
                            return Task.CompletedTask;
                        default:
                            return Task.CompletedTask;
                    }
                }
            ).WithSenderMiddleware((Func<Sender, Sender>)PropagateHeaders);

            PID pid1 = system.Root.Spawn(props1);

            TaskCompletionSource<MessageHeader> tcs1 = new TaskCompletionSource<MessageHeader>();
            TaskCompletionSource<MessageHeader> tcs2 = new TaskCompletionSource<MessageHeader>();
            Props props2 = Props.FromFunc(ctx =>
                {
                    switch (ctx.Message)
                    {
                        case StartMessage:
                            tcs1.SetResult(ctx.Headers);
                            ctx.Request(pid1, new SomeRequest());
                            break;
                        case SomeResponse:
                            tcs2.SetResult(ctx.Headers);
                            break;
                    }

                    return Task.CompletedTask;
                }
            ).WithSenderMiddleware((Func<Sender, Sender>)PropagateHeaders);

            PID pid2 = system.Root.Spawn(props2);

            //ensure we set the headers up correctly
            MessageHeader headers = MessageHeader.Empty.With("foo", "bar");
            Assert.Equal("bar", headers.GetOrDefault("foo"));

            //use the headers and send the request
            RootContext root = system
                .Root
                .WithHeaders(headers)
                .WithSenderMiddleware((Func<Sender, Sender>)PropagateHeaders);

            root.Send(pid2, new StartMessage());

            //actor1 should have headers
            MessageHeader headers1 = await tcs1.Task.WithTimeout(TimeSpan.FromSeconds(5));
            Assert.Equal(headers, headers1);

            //actor2 should have headers
            MessageHeader headers2 = await tcs2.Task.WithTimeout(TimeSpan.FromSeconds(5));
            Assert.Equal(headers, headers2);
        }

        public record SomeRequest;

        public record SomeResponse;

        public record StartMessage;
    }
}
