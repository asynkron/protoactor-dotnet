using System.Collections.Generic;
using System.Threading.Tasks;
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
        private readonly ISpanContext _spanContext;
        private readonly ISpan _span;
        private readonly IScope _scope;
        private readonly ISpanBuilder _spanBuilder;
        private readonly ITracer _tracer;
        private ActorSystem System;

        public OpenTracingExtensionsTests()
        {
            System = new ActorSystem();
            _spanContext = Substitute.For<ISpanContext>();

            _span = Substitute.For<ISpan>();
            _span.Context.Returns(_spanContext);

            _scope = Substitute.For<IScope>();
            _scope.Span.Returns(_span);

            _spanBuilder = Substitute.For<ISpanBuilder>();
            _spanBuilder.AsChildOf(Arg.Any<ISpan>()).Returns(_spanBuilder);
            _spanBuilder.StartActive().Returns(_scope);
            _spanBuilder.StartActive(Arg.Any<bool>()).ReturnsForAnyArgs(_scope);

            _tracer = Substitute.For<ITracer>();
            _tracer.BuildSpan("").ReturnsForAnyArgs(_spanBuilder);
        }

        [Fact]
        public void OpenTracingReceiverTest()
        {
            var messages = new List<object>();

            var actorProps = Props
                .FromFunc(ctx => { messages.Add(ctx.Message); return Actor.Done; })
                .WithMailbox(() => new TestMailbox())
                .WithOpenTracing(tracer: _tracer)
                ;

            var actor = System.Root.Spawn(actorProps);

            System.Root.Send(actor, "test_message");

            Assert.Equal(2, messages.Count); // Started & "test_message"

            _tracer.ReceivedWithAnyArgs(2).BuildSpan(null);
            _tracer.Received(1).BuildSpan("Receive Started");
            _tracer.Received(1).BuildSpan("Receive String");
        }

        [Fact]
        public void RootContextOpenTracingSenderTest()
        {
            var root = new RootContext(System, new MessageHeader(), OpenTracingExtensions.OpenTracingSenderMiddleware(_tracer))
                .WithOpenTracing(tracer: _tracer);

            var messages = new List<object>();

            var actorProps = Props
                .FromFunc(ctx => { messages.Add(ctx.Message); return Actor.Done; })
                .WithMailbox(() => new TestMailbox())
                ;
            var actor = System.Root.Spawn(actorProps);

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
                .FromFunc(ctx => { messages.Add(ctx.Message); return Actor.Done; })
                .WithMailbox(() => new TestMailbox())
                ;
            var finalTarget = System.Root.Spawn(finalTargetProps);

            var actorProps = Props
                .FromFunc(ctx =>
                {
                    if (ctx.Message is string msg)
                        switch (msg)
                        {
                            case "send": ctx.Send(finalTarget, msg); break;
                            case "request": ctx.Request(finalTarget, msg); break;
                            case "forward": ctx.Forward(finalTarget); break;
                        }
                    return Actor.Done;
                })
                .WithMailbox(() => new TestMailbox())
                // This is OpenTracing stack without received
                .WithContextDecorator(ctx => ctx.WithOpenTracing(null, tracer: _tracer))
                .WithOpenTracingSender(_tracer)
                ;
            var actor = System.Root.Spawn(actorProps);

            System.Root.Send(actor, "send");
            System.Root.Send(actor, "request");
            System.Root.Send(actor, "forward");

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
