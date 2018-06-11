using NSubstitute;
using OpenTracing;
using OpenTracing.Noop;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.OpenTracing.Tests
{
    public class OpenTracingExtensionsTests
    {
        [Theory]
        [InlineData("traceid", "spanId", true)]
        [InlineData(null, null, false)]
        [InlineData("traceid", null, false)]
        [InlineData(null, "spanId", false)]
        public async Task WithOpenTracingSenderTest(string traceId, string spanId, bool spanCtxExpected)
        {
            // GIVEN
            var spanContext = Substitute.For<ISpanContext>();
            spanContext.TraceId.Returns(traceId);
            spanContext.SpanId.Returns(spanId);

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
                (sp, message) => span.Log(message.ToString()),
                tracer
                );

            var senderPid = new PID("here", "sender");
            var targetPid = new PID("here", "target");


            // WHEN
            await sendMiddleware(async (context, target, envelope) =>
                {
                    // THEN
                    Assert.Null(context);
                    Assert.Equal(targetPid, target);
                    Assert.Equal(senderPid, envelope.Sender);

                    var hasSpanCtx = envelope.Header.TryGetSpanContext(out var spContext);
                    Assert.Equal(hasSpanCtx, spanCtxExpected);

                    if (spanCtxExpected)
                    {
                        Assert.Equal(traceId, spContext.TraceId);
                        Assert.Equal(spanId, spContext.SpanId);
                    }
                    else
                    {
                        Assert.Null(spanContext);
                    }
                })
            (null, targetPid, new MessageEnvelope("test", senderPid, new MessageHeader()));
        }
    }
}
