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
        /// <summary>
        /// Setup open tracing send middleware & decorator.
        /// </summary>
        /// <param name="props">props.</param>
        /// <param name="sendSpanSetup">provide a way inject send span constumisation according to the message.</param>
        /// <param name="receiveSpanSetup">provide a way inject receive span constumisation according to the message.</param>
        /// <param name="tracer">OpenTracing, if nul : GlobalTracer.Instance will be used.</param>
        /// <returns>props</returns>
        public static Props WithOpenTracing(this Props props, SpanSetup sendSpanSetup = null, SpanSetup receiveSpanSetup = null, ITracer tracer = null)
            => props
                .WithContextDecorator(ctx => ctx.WithOpenTracing(sendSpanSetup, receiveSpanSetup, tracer))
                .WithReceiveMiddleware(next => (ctx, envelop) => next(ctx, envelop)) // this is only to trigger the decorator usage
                .WithOpenTracingSender(tracer)
                //.WithOpenTracingReceiver(receiveSpanSetup, tracer)
            ;

        internal static Props WithOpenTracingSender(this Props props, ITracer tracer)
            => props.WithSenderMiddleware(OpenTracingSenderMiddleware(tracer));

        /// <summary>
        /// Only responsible to tweak the envelop in order to send SpanContext informations.
        /// </summary>
        public static Func<Sender, Sender> OpenTracingSenderMiddleware(ITracer tracer = null)
            => next => async (context, target, envelope) =>
            {
                tracer = tracer ?? GlobalTracer.Instance;

                var span = tracer.ActiveSpan;

                Task simpleNext() => next(context, target, envelope); // to forget nothing

                if (span == null)
                {
                    await simpleNext().ConfigureAwait(false);
                }
                else
                {
                    var dictionary = new Dictionary<string, string>();
                    tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(dictionary));
                    envelope = envelope.WithHeaders(dictionary);

                    await simpleNext().ConfigureAwait(false);
                }
            };

        //internal static Props WithOpenTracingReceiver(this Props props, SpanSetup receiveSpanSetup, ITracer tracer)
        //   => props.WithReceiveMiddleware(OpenTracingReceiverMiddleware(receiveSpanSetup, tracer));

        //public static Func<Receiver, Receiver> OpenTracingReceiverMiddleware(SpanSetup receiveSpanSetup = null, ITracer tracer = null)
        //    => next => async (context, envelope) =>
        //    {
        //        receiveSpanSetup = receiveSpanSetup ?? OpenTracingHelpers.DefaultSetupSpan;
        //        tracer = tracer ?? GlobalTracer.Instance;

        //        var message = envelope.Message;

        //        var parentSpanCtx = envelope.Header != null
        //          ? tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(envelope.Header.ToDictionary()))
        //          : null;

        //        using (var scope = tracer.BuildStartedScope(parentSpanCtx, nameof(Receive), message, receiveSpanSetup))
        //        {
        //            try
        //            {
        //                var span = scope.Span;

        //                if (envelope.Sender != null) ProtoTags.SenderPID.Set(span, envelope.Sender.ToShortString());

        //                receiveSpanSetup?.Invoke(span, message);

        //                await next(context, envelope).ConfigureAwait(false);
        //            }
        //            catch (Exception ex)
        //            {
        //                ex.SetupSpan(scope.Span);
        //                throw;
        //            }
        //        }
        //    };

        internal static IContext WithOpenTracing(this IContext context, SpanSetup sendSpanSetup = null, SpanSetup receiveSpanSetup = null, ITracer tracer = null)
        {
            sendSpanSetup = sendSpanSetup ?? OpenTracingHelpers.DefaultSetupSpan;
            receiveSpanSetup = receiveSpanSetup ?? OpenTracingHelpers.DefaultSetupSpan;
            tracer = tracer ?? GlobalTracer.Instance;

            return new OpenTracingActorContextDecorator(context, sendSpanSetup, receiveSpanSetup, tracer);
        }

        /// <summary>
        /// Setup open tracing send decorator around RootContext.
        /// DO NOT FORGET to create the RootContext passing OpenTracingExtensions.OpenTracingSenderMiddleware to the constructor.
        /// </summary>
        /// <param name="props">props.</param>
        /// <param name="sendSpanSetup">provide a way inject send span constumisation according to the message.</param>
        /// <param name="tracer">OpenTracing, if nul : GlobalTracer.Instance will be used.</param>
        /// <returns>IRootContext</returns>
        public static IRootContext WithOpenTracing(this IRootContext context, SpanSetup sendSpanSetup = null, ITracer tracer = null)
        {
            sendSpanSetup = sendSpanSetup ?? OpenTracingHelpers.DefaultSetupSpan;
            tracer = tracer ?? GlobalTracer.Instance;

            return new OpenTracingRootContextDecorator(context, sendSpanSetup, tracer);
        }
    }
}
