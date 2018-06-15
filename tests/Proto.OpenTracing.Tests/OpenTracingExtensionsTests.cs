using System.Threading.Tasks;
using NSubstitute;
using OpenTracing;
using Xunit;

namespace Proto.OpenTracing.Tests
{
    public class OpenTracingExtensionsTests
    {
        [Fact]
        public async Task OpenTracingReceiverMiddlewareTests()
        {
            // GIVEN
            var spanContext = Substitute.For<ISpanContext>();

            var span = Substitute.For<ISpan>();
            span.Context.Returns(spanContext);

            var scope = Substitute.For<IScope>();
            scope.Span.Returns(span);

            var spanBuilder = Substitute.For<ISpanBuilder>();
            spanBuilder.AsChildOf(Arg.Any<ISpan>()).Returns(spanBuilder);
            spanBuilder.StartActive().Returns(scope);
            spanBuilder.StartActive(Arg.Any<bool>()).ReturnsForAnyArgs(scope);

            var tracer = Substitute.For<ITracer>();
            tracer.BuildSpan("").ReturnsForAnyArgs(spanBuilder);

            var receiveMiddleware = OpenTracingExtensions.OpenTracingReceiverMiddleware(
                (sp, message) => span.Log(message.ToString()),
                tracer
            );

            var senderPid = new PID("here", "sender");

            var env = new MessageEnvelope("test", senderPid, new MessageHeader());

            // WHEN
            var receive = receiveMiddleware((context, envelope) =>
            {
                // THEN
                Assert.Null(context);
                Assert.Equal(senderPid, envelope.Sender);

                span.Received(1).Log("test");
                return Actor.Done;
            });

            await receive(null, env);
        }

        [Fact]
        public async Task OpenTracingSenderMiddlewareTest()
        {
            // GIVEN
            var spanContext = Substitute.For<ISpanContext>();

            var span = Substitute.For<ISpan>();
            span.Context.Returns(spanContext);

            var scope = Substitute.For<IScope>();
            scope.Span.Returns(span);

            var spanBuilder = Substitute.For<ISpanBuilder>();
            spanBuilder.AsChildOf(Arg.Any<ISpan>()).Returns(spanBuilder);
            spanBuilder.StartActive().Returns(scope);
            spanBuilder.StartActive(Arg.Any<bool>()).ReturnsForAnyArgs(scope);

            var tracer = Substitute.For<ITracer>();
            tracer.BuildSpan("").ReturnsForAnyArgs(spanBuilder);

            var sendMiddleware = OpenTracingExtensions.OpenTracingSenderMiddleware(
                tracer
            );

            var senderPid = new PID("here", "sender");
            var targetPid = new PID("here", "target");


            // WHEN
            await sendMiddleware((context, target, envelope) =>
                {
                    // THEN
                    Assert.Null(context);
                    Assert.Equal(targetPid, target);
                    Assert.Equal(senderPid, envelope.Sender);

                    span.Received(1).Log("test");
                    return Actor.Done;
                })
                (null, targetPid, new MessageEnvelope("test", senderPid, new MessageHeader()));
        }
    }
}