using OpenTracing;
using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    static class OpenTracingHelpers
    {
        public static IScope BuildStartedScope(this ITracer tracer, ISpanContext parentSpan, string verb, object message, SpanSetup spanSetup)
        {
            var scope = tracer
                .BuildSpan($"{verb} {message?.GetType().Name ?? "Unknown"}")
                .AsChildOf(parentSpan)
                .StartActive(finishSpanOnDispose: true);

            spanSetup?.Invoke(scope.Span, message);

            return scope;
        }

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
