using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Proto.OpenTracing
{
    public delegate void SpanSetup(ISpan span, object message);

    /// <summary>
    /// Good documentation/tutorials here : https://github.com/yurishkuro/opentracing-tutorial/tree/master/csharp
    /// </summary>
    public static class OpenTracingExtensions
    {
        public static Props WithOpenTracing(this Props props, SpanSetup sendSpanSetup = null, SpanSetup receiveSpanSetup = null, ITracer tracer = null)
            => props
                .WithOpenTracingSender(sendSpanSetup, tracer)
                .WithOpenTracingReceiver(receiveSpanSetup, tracer)
            ;

        internal static Props WithOpenTracingSender(this Props props, SpanSetup sendSpanSetup, ITracer tracer)
            => props.WithSenderMiddleware(OpenTracingSenderMiddleware(sendSpanSetup, tracer));

        public static Func<Sender, Sender> OpenTracingSenderMiddleware(SpanSetup sendSpanSetup = null, ITracer tracer = null)
            => next => async (context, target, envelope) =>
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

                        var dictionary = new Dictionary<string, string>();
                        tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(dictionary));
                        envelope = envelope.WithHeaders(dictionary);

                        await next(context, target, envelope).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Tags.Error.Set(span, true);
                        span.Log(ex.Message);
                        span.Log(ex.StackTrace);
                        throw;
                    }
                }
            };

        internal static Props WithOpenTracingReceiver(this Props props, SpanSetup receiveSpanSetup, ITracer tracer)
            => props.WithReceiveMiddleware(OpenTracingReceiverMiddleware(receiveSpanSetup, tracer));

        public static Func<Receiver, Receiver> OpenTracingReceiverMiddleware(SpanSetup receiveSpanSetup = null, ITracer tracer = null)
            => next => async (context, envelope) =>
            {
                tracer = tracer ?? GlobalTracer.Instance;

                var message = envelope.Message;

                var parentSpanCtx = envelope.Header != null
                    ? tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(envelope.Header.ToDictionary()))
                    : null;
                
                using (IScope scope = tracer
                    .BuildSpan("Receive " + (message?.GetType().Name ?? "Unknown"))
                    .AsChildOf(parentSpanCtx)
                    .StartActive(finishSpanOnDispose: true)
                    )
                {
                    var span = scope.Span;

                    try
                    {
                        receiveSpanSetup?.Invoke(span, message);

                        await next(context, envelope).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Tags.Error.Set(span, true);
                        span.Log(ex.Message);
                        span.Log(ex.StackTrace);
                        throw;
                    }


                    // No need to call scope.Span.Finish() as we've set finishSpanOnDispose:true in StartActive.
                }
            };
    }
}
