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
                .WithContextDecorator(ctx => ctx.WithOpenTracing(sendSpanSetup, tracer))
                .WithOpenTracingSender(tracer)
                .WithOpenTracingReceiver(receiveSpanSetup, tracer)
            ;

        internal static Props WithOpenTracingSender(this Props props, ITracer tracer)
            => props.WithSenderMiddleware(OpenTracingSenderMiddleware(tracer));

        public static Func<Sender, Sender> OpenTracingSenderMiddleware(ITracer tracer = null)
            => next => async (context, target, envelope) =>
            {
                tracer = tracer ?? GlobalTracer.Instance;

                var span = tracer.ActiveSpan;
                Task simpleNext() => next(context, target, envelope);

                if (span == null)
                    await simpleNext().ConfigureAwait(false);
                else
                {
                    try
                    {
                        ProtoTags.TargetPID.Set(span, target.ToShortString());
                        if (envelope.Sender != null) ProtoTags.SenderPID.Set(span, envelope.Sender.ToShortString());

                        var dictionary = new Dictionary<string, string>();
                        tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(dictionary));
                        envelope = envelope.WithHeaders(dictionary);

                        await simpleNext().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Tags.Error.Set(span, true);
                        var baseEx = ex.GetBaseException();
                        span.Log(new Dictionary<string, object> {
                            { "exception", baseEx.GetType().Name },
                            { "message", baseEx.Message },
                            { "stackTrace", ex.StackTrace },
                        });
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

                using (var scope = tracer.BuildStartedScope(parentSpanCtx, "Receive", message, receiveSpanSetup))
                {
                    var span = scope.Span;

                    try
                    {
                        if (envelope.Sender != null) ProtoTags.SenderPID.Set(span, envelope.Sender.ToShortString());

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
                }
            };

        static IContext WithOpenTracing(this IContext context, SpanSetup sendSpanSetup = null, ITracer tracer = null)
        {
            tracer = tracer ?? GlobalTracer.Instance;
            return new OpenTracingActorContextDecorator(context, sendSpanSetup, tracer);
        }

        public static IRootContext WithOpenTracing(this IRootContext context, SpanSetup sendSpanSetup = null, ITracer tracer = null)
        {
            tracer = tracer ?? GlobalTracer.Instance;
            return new OpenTracingRootContextDecorator(context, sendSpanSetup, tracer);
        }
    }
}
