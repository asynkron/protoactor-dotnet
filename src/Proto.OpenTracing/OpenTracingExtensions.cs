﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;

namespace Proto.OpenTracing
{
    public delegate void SpanSetup(ISpan span, object message);

    /// <summary>
    ///     Good documentation/tutorials here : https://github.com/yurishkuro/opentracing-tutorial/tree/master/csharp
    /// </summary>
    [Obsolete(ObsoleteInformation.Text)]
    public static class OpenTracingExtensions
    {
        /// <summary>
        ///     Setup open tracing send middleware & decorator.
        /// </summary>
        /// <param name="props">props.</param>
        /// <param name="sendSpanSetup">provide a way inject send span customization according to the message.</param>
        /// <param name="receiveSpanSetup">provide a way inject receive span customization according to the message.</param>
        /// <param name="tracer">OpenTracing, if nul : GlobalTracer.Instance will be used.</param>
        /// <returns>props</returns>
        [Obsolete(ObsoleteInformation.Text)]
        public static Props WithOpenTracing(
            this Props props,
            SpanSetup sendSpanSetup = null,
            SpanSetup receiveSpanSetup = null,
            ITracer tracer = null
        )
            => props
                .WithContextDecorator(ctx => ctx.WithOpenTracing(sendSpanSetup, receiveSpanSetup, tracer))
                .WithOpenTracingSender(tracer);

        internal static Props WithOpenTracingSender(this Props props, ITracer tracer)
            => props.WithSenderMiddleware(OpenTracingSenderMiddleware(tracer));

        /// <summary>
        ///     Only responsible to tweak the envelop in order to send SpanContext informations.
        /// </summary>
        [Obsolete(ObsoleteInformation.Text)]
        public static Func<Sender, Sender> OpenTracingSenderMiddleware(ITracer tracer = null)
            => next => async (context, target, envelope) => {
                tracer ??= GlobalTracer.Instance;

                var span = tracer.ActiveSpan;

                Task SimpleNext() => next(context, target, envelope); // to forget nothing

                if (span == null) await SimpleNext().ConfigureAwait(false);
                else
                {
                    var dictionary = new Dictionary<string, string>();
                    tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(dictionary));
                    envelope = envelope.WithHeaders(dictionary);

                    await SimpleNext().ConfigureAwait(false);
                }
            };

        internal static IContext WithOpenTracing(
            this IContext context,
            SpanSetup sendSpanSetup = null,
            SpanSetup receiveSpanSetup = null,
            ITracer tracer = null
        )
        {
            sendSpanSetup ??= OpenTracingHelpers.DefaultSetupSpan;
            receiveSpanSetup ??= OpenTracingHelpers.DefaultSetupSpan;
            tracer ??= GlobalTracer.Instance;

            return new OpenTracingActorContextDecorator(context, sendSpanSetup, receiveSpanSetup, tracer);
        }

        /// <summary>
        ///     Setup open tracing send decorator around RootContext.
        ///     DO NOT FORGET to create the RootContext passing OpenTracingExtensions.OpenTracingSenderMiddleware to the
        ///     constructor.
        /// </summary>
        /// <param name="props">props.</param>
        /// <param name="sendSpanSetup">provide a way inject send span customization according to the message.</param>
        /// <param name="tracer">OpenTracing, if nul : GlobalTracer.Instance will be used.</param>
        /// <returns>IRootContext</returns>
        [Obsolete(ObsoleteInformation.Text)]
        public static IRootContext WithOpenTracing(this IRootContext context, SpanSetup sendSpanSetup = null, ITracer tracer = null)
        {
            sendSpanSetup ??= OpenTracingHelpers.DefaultSetupSpan;
            tracer ??= GlobalTracer.Instance;

            return new OpenTracingRootContextDecorator(context, sendSpanSetup, tracer);
        }
    }
}