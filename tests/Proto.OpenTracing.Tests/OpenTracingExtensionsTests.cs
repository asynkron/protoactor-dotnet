using System.Collections.Generic;
using NSubstitute;
using OpenTracing;
using Proto.TestFixtures;
using Xunit;

namespace Proto.OpenTracing.Tests
{
    /// <summary>
    /// Some OpenTracing sanity checks
    /// </summary>
    public class OpenTracingExtensionsTests
    {
        private readonly ITracer _tracer;

        public OpenTracingExtensionsTests()
        {
            var spanContext = Substitute.For<ISpanContext>();

            var span = Substitute.For<ISpan>();
            span.Context.Returns(spanContext);

            var scope = Substitute.For<IScope>();
            scope.Span.Returns(span);

            var spanBuilder = Substitute.For<ISpanBuilder>();
            spanBuilder.AsChildOf(Arg.Any<ISpan>()).Returns(spanBuilder);
            spanBuilder.StartActive().Returns(scope);
            spanBuilder.StartActive(Arg.Any<bool>()).ReturnsForAnyArgs(scope);

            _tracer = Substitute.For<ITracer>();
            _tracer.BuildSpan("").ReturnsForAnyArgs(spanBuilder);
        }

        [Fact]
        public void OpenTracingReceiverTest()
        {
            var messages = new List<object>();

            var actorProps = Props
                .FromFunc(
                    ctx =>
                    {
                        messages.Add(ctx.Message);
                        return Actor.Done;
                    }
                )
                .WithMailbox(() => new TestMailbox())
                .WithOpenTracing(tracer: _tracer);

            var actor = RootContext.Empty.Spawn(actorProps);

            RootContext.Empty.Send(actor, "test_message");

            Assert.Equal(2, messages.Count); // Started & "test_message"

            _tracer.ReceivedWithAnyArgs(2).BuildSpan(null);
            _tracer.Received(1).BuildSpan("Receive Started");
            _tracer.Received(1).BuildSpan("Receive String");
        }

        [Fact]
        public void RootContextOpenTracingSenderTest()
        {
            var root = new RootContext(new MessageHeader(), OpenTracingExtensions.OpenTracingSenderMiddleware(_tracer))
                .WithOpenTracing(tracer: _tracer);

            var messages = new List<object>();

            var actorProps = Props
                .FromFunc(
                    ctx =>
                    {
                        messages.Add(ctx.Message);
                        return Actor.Done;
                    }
                )
                .WithMailbox(() => new TestMailbox());
            var actor = RootContext.Empty.Spawn(actorProps);

            root.Send(actor, "test_message");

            Assert.Equal(2, messages.Count); // Started & "test_message"
            _tracer.Received(1).BuildSpan("Send String");

            _tracer.ClearReceivedCalls();

            root.Request(actor, "test_message_2");
            Assert.Equal(3, messages.Count); // Started & "test_message" & "test_message_2"
            _tracer.Received(1).BuildSpan("Request String");
        }

        [Fact]
        public void ActorContextOpenTracingSenderTest()
        {
            var messages = new List<object>();

            var finalTargetProps = Props
                    .FromFunc(
                        ctx =>
                        {
                            messages.Add(ctx.Message);
                            return Actor.Done;
                        }
                    )
                    .WithMailbox(() => new TestMailbox())
                ;
            var finalTarget = RootContext.Empty.Spawn(finalTargetProps);

            var actorProps = Props
                    .FromFunc(
                        ctx =>
                        {
                            if (!(ctx.Message is string msg)) return Actor.Done;

                            switch (msg)
                            {
                                case "send":
                                    ctx.Send(finalTarget, msg);
                                    break;
                                case "request":
                                    ctx.Request(finalTarget, msg);
                                    break;
                                case "forward":
                                    ctx.Forward(finalTarget);
                                    break;
                            }

                            return Actor.Done;
                        }
                    )
                    .WithMailbox(() => new TestMailbox())
                    // This is OpenTracing stack without received
                    .WithContextDecorator(ctx => ctx.WithOpenTracing(null, tracer: _tracer))
                    .WithOpenTracingSender(_tracer)
                ;
            var actor = RootContext.Empty.Spawn(actorProps);

            RootContext.Empty.Send(actor, "send");
            RootContext.Empty.Send(actor, "request");
            RootContext.Empty.Send(actor, "forward");

            Assert.Equal(4, messages.Count); // Started & "send" & "request" & "forward"
            Assert.Equal("send", messages[1]);
            Assert.Equal("request", messages[2]);
            Assert.Equal("forward", messages[3]);

            _tracer.Received(1).BuildSpan("Send String");
            _tracer.Received(1).BuildSpan("Request String");
            _tracer.Received(1).BuildSpan("Forward String");
        }
    }
}