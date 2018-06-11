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
        public const string OpenTracingHeaderKey = "OpenTracingContext";



        public static Props WithOpenTracing(this Props props, SpanSetup sendSpanSetup, SpanSetup receiveSpanSetup, ITracer tracer = null)
            => props
                .WithOpenTracingSender(sendSpanSetup, tracer)
                .WithOpenTracingReceiver(receiveSpanSetup, tracer)
            ;

        private static Props WithOpenTracingSender(this Props props, SpanSetup sendSpanSetup, ITracer tracer) => props.WithSenderMiddleware(OpenTracingSenderMiddleware(sendSpanSetup, tracer));

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

                    envelope = envelope
                        .WithHeader(OpenTracingHeaderKey, SpanContext.ToHeader(span.Context)
                        );

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

        private static Props WithOpenTracingReceiver(this Props props, SpanSetup receiveSpanSetup, ITracer tracer) =>
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


        private class SpanContext : ISpanContext
        {
            public static string ToHeader(ISpanContext context)
            {
                var sb = new StringBuilder()
                    .Append(context.TraceId).Append("|").Append(context.SpanId);

                // TODO : GRPC serialisation ?
                //foreach (var (key, value) in context.GetBaggageItems())
                //{
                //    sc.BaggageItems[key] = value;
                //}

                return sb.ToString();
            }

            public static SpanContext FromHeader(string headerValue)
            {
                var ids = headerValue.Split('|');
                return new SpanContext { TraceId = ids[0], SpanId = ids[1] };
            }

            public string TraceId { get; private set; }

            public string SpanId { get; private set; }

            // TODO
            //public Dictionary<string, string> BaggageItems { get; } = new Dictionary<string, string>();

            public IEnumerable<KeyValuePair<string, string>> GetBaggageItems() { yield break; }
        }

        private static bool TryGetSpanContext(this MessageHeader header, out SpanContext spanContext)
        {
            spanContext = default;
            if (header == null) return false;

            var contextValues = header.GetOrDefault(OpenTracingHeaderKey);
            if (contextValues == null) return false;

            spanContext = SpanContext.FromHeader(contextValues);
            return true;
        }
    }
}
