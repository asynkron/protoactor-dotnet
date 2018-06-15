using OpenTracing;
using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Proto.OpenTracing
{
    static class OpenTracingHelpers
    {
        public static void DefaultSetupSpan(ISpan span, object message) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IScope BuildStartedScope(this ITracer tracer, ISpanContext parentSpan, string verb, object message, SpanSetup spanSetup)
        {
            var messageType = message?.GetType().Name ?? "Unknown";

            var scope = tracer
                .BuildSpan($"{verb} {messageType}") // <= perhaps is not good to have the operation name mentioning the message type
                .AsChildOf(parentSpan)
                .StartActive(finishSpanOnDispose: true);

            ProtoTags.MessageType.Set(scope.Span, messageType);

            spanSetup?.Invoke(scope.Span, message);

            return scope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetupSpan(this Exception exception, ISpan span)
        {
            if (span == null) return;

            Tags.Error.Set(span, true);
            var baseEx = exception.GetBaseException();
            span.Log(
                new Dictionary<string, object> {
                    { "exception", baseEx.GetType().Name },
                    { "message", baseEx.Message },
                    { "stackTrace", exception.StackTrace },
                });
        }
    }
}
