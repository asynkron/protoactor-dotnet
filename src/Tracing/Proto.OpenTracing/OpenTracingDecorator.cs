using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenTracing;

namespace Proto.OpenTracing
{
    class OpenTracingDecorator<T> : DispatchProxy
    {
        private OpenTracingDecorator() { }

        T _context;

        public static T Create<T>(T context, SpanSetup sendSpanSetup, ITracer tracer)
        {
            var proxy = Create<T, OpenTracingDecorator<T>>();
            var t = proxy as OpenTracingDecorator<T>;
            t._context = context;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == nameof(ISenderContext.RequestAsync))
                throw new InvalidCastException("Just to know I passed here");

            return targetMethod.Invoke(_context, args);
        }
    }
}
