using OpenTracing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    static class SpanContextExtensions
    {
        public const string OpenTracingHeaderKey = "OpenTracingContext";

        public static MessageEnvelope WithSpanContextHeader(this MessageEnvelope envelope, ISpanContext spanContext)
        {
            if (spanContext.TraceId == null || spanContext.SpanId == null) return envelope;

            var sb = new StringBuilder()
                .Append(spanContext.TraceId).Append("|").Append(spanContext.SpanId);

            // TODO : GRPC serialisation ?
            //foreach (var (key, value) in context.GetBaggageItems())
            //{
            //    sc.BaggageItems[key] = value;
            //}

            return envelope.WithHeader(OpenTracingHeaderKey, sb.ToString());
        }

        public static bool TryGetSpanContext(this MessageHeader header, out SpanContext spanContext)
        {
            spanContext = default;
            if (header == null) return false;

            var contextValues = header.GetOrDefault(OpenTracingHeaderKey);
            if (contextValues == null) return false;

            var ids = contextValues.Split('|');

            spanContext = new SpanContext(ids[0], ids[1]);
            return true;
        }
    }
}
