using OpenTracing;
using OpenTracing.Tag;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Proto.OpenTracing
{
    public delegate void SpanSetup(ISpan span, object message);

    public static class OpenTracingExtensions
    {
        public static Props WithOpenTracing(this Props props, SpanSetup sendSpanSetup = null, SpanSetup receiveSpanSetup = null, ITracer tracer = null)
            => props
                .WithOpenTracingSender(sendSpanSetup, tracer)
                .WithOpenTracingReceiver(receiveSpanSetup, tracer)
            ;

        internal static Props WithOpenTracingSender(this Props props, SpanSetup sendSpanSetup, ITracer tracer)
            => props.WithSenderMiddleware(OpenTracingSenderMiddleware(sendSpanSetup, tracer));

        public static Func<Sender, Sender> OpenTracingSenderMiddleware(SpanSetup sendSpanSetup, ITracer tracer) => next => async (context, target, envelope) =>
        {
            tracer = tracer ?? GlobalTracer.Instance;

            var message = envelope.Message;

            using (IScope scope = tracer
                .BuildSpan("Send " + (message?.GetType().Name ?? "Unknown"))
                .AsChildOf(tracer.ActiveSpan)
                .StartActive(finishSpanOnDispose: true)
                )
            {
                var span = scope.Span;

                try
                {
                    sendSpanSetup?.Invoke(span, message);

                    envelope = envelope.WithSpanContextHeader(span.Context);

                    await next(context, target, envelope).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Tags.Error.Set(scope.Span, true);
                    span.Log(ex.Message);
                    span.Log(ex.StackTrace);
                    throw;
                }
            }
        };

        internal static Props WithOpenTracingReceiver(this Props props, SpanSetup receiveSpanSetup, ITracer tracer) =>
            props.WithReceiveMiddleware(next => async (context, envelope) =>
            {
                tracer = tracer ?? GlobalTracer.Instance;

                var message = envelope.Message;

                envelope.Header.TryGetSpanContext(out var parentSpanContext);

                using (IScope scope = tracer
                    .BuildSpan("Receive " + (message?.GetType().Name ?? "Unknown"))
                    .AsChildOf(parentSpanContext)
                    .StartActive(finishSpanOnDispose: true)
                    )
                {
                    var span = scope.Span;

                    receiveSpanSetup?.Invoke(span, message);

                    try
                    {
                        await context.Receive(envelope).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Tags.Error.Set(scope.Span, true);
                        scope.Span.Log(ex.Message);
                        scope.Span.Log(ex.StackTrace);
                        throw;
                    }

                    // No need to call scope.Span.Finish() as we've set finishSpanOnDispose:true in StartActive.
                }
            });
    }
}
