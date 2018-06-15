using System.Threading.Tasks;
using NSubstitute;
using OpenTracing;
using Xunit;

namespace Proto.OpenTracing.Tests
{
    public class OpenTracingExtensionsTests
    {
        private readonly ISpanContext _spanContext;
        private readonly ISpan _span;
        private readonly IScope _scope;
        private readonly ISpanBuilder _spanBuilder;
        private readonly ITracer _tracer;

        public OpenTracingExtensionsTests()
        {
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
        public async Task OpenTracingReceiverMiddlewareTests()
        {
            var actorProps = Props
                .FromFunc(ctx => Actor.Done)
                .WithOpenTracing(tracer: _tracer);

            var actor = RootContext.Empty.SpawnNamed(actorProps, "hello");

            RootContext.Empty.Send(actor, "test_message");


            //Assert.Null(context);
            //Assert.Equal(senderPid, envelope.Sender);

            _span.Received(1).SetTag(ProtoTags.ActorType.Key, "<None>");

        }

        [Fact]
        public async Task OpenTracingSenderMiddlewareTest()
        {
            // GIVEN
            var sendMiddleware = OpenTracingExtensions.OpenTracingSenderMiddleware(_tracer);

            var senderPid = new PID("here", "sender");
            var targetPid = new PID("here", "target");


            // WHEN
            await sendMiddleware((context, target, envelope) =>
                {
                    // THEN
                    Assert.Null(context);
                    Assert.Equal(targetPid, target);
                    Assert.Equal(senderPid, envelope.Sender);

                    _span.Received(1).Log("test");
                    return Actor.Done;
                })
                (null, targetPid, new MessageEnvelope("test", senderPid, new MessageHeader()));
        }
    }
}