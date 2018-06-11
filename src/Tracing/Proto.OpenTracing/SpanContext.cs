using OpenTracing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    class SpanContext : ISpanContext
    {
        public SpanContext(string traceId, string spanId)
        {
            TraceId = traceId;
            SpanId = spanId;
        }

        public string TraceId { get; private set; }

        public string SpanId { get; private set; }

        // TODO
        //public Dictionary<string, string> BaggageItems { get; } = new Dictionary<string, string>();

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems() { yield break; }
    }


}
