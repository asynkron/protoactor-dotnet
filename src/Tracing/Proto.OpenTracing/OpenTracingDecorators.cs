using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTracing;
using OpenTracing.Tag;

namespace Proto.OpenTracing
{
    internal class OpenTracingRootContextDecorator : RootContextDecorator
    {
        private readonly SpanSetup _sendSpanSetup;
        private readonly ITracer _tracer;

        public OpenTracingRootContextDecorator(IRootContext context, SpanSetup sendSpanSetup, ITracer tracer) : base(context)
        {
            _sendSpanSetup = (span, message) =>
            {
                ProtoTags.ActorType.Set(span, "<None>");
                sendSpanSetup(span, message);
            };

            _tracer = tracer;
        }

        public override void Send(PID target, object message)
            => OpenTracingMethodsDecorators.Send(message, _sendSpanSetup, _tracer, () => base.Send(target, message));

        public override void Request(PID target, object message)
            => OpenTracingMethodsDecorators.Request(message, _sendSpanSetup, _tracer, () => base.Request(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message, timeout));

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message, cancellationToken));
    }

    internal class OpenTracingActorContextDecorator : ActorContextDecorator
    {
        private readonly SpanSetup _sendSpanSetup;
        private readonly ITracer _tracer;

        public OpenTracingActorContextDecorator(IContext context, SpanSetup sendSpanSetup, ITracer tracer) : base(context)
        {
            _sendSpanSetup = (span, message) =>
            {
                ProtoTags.ActorType.Set(span, context.Actor.GetType().Name);
                ProtoTags.SenderPID.Set(span, context.Self.ToShortString());
                sendSpanSetup(span, message);
            };

            _tracer = tracer;
        }

        public override void Send(PID target, object message)
            => OpenTracingMethodsDecorators.Send(message, _sendSpanSetup, _tracer, () => base.Send(target, message));

        public override void Request(PID target, object message)
            => OpenTracingMethodsDecorators.Request(message, _sendSpanSetup, _tracer, () => base.Request(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message, timeout));

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTracingMethodsDecorators.RequestAsync(message, _sendSpanSetup, _tracer, () => base.RequestAsync<T>(target, message, cancellationToken));
    }

    static class OpenTracingMethodsDecorators
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Send(object message, SpanSetup sendSpanSetup, ITracer tracer, Action send)
        {
            using (var scope = tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Send), message, sendSpanSetup))
            {
                try
                {
                    send();
                }
                catch (Exception ex)
                {
                    ex.SetupSpan(scope.Span);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Request(object message, SpanSetup sendSpanSetup, ITracer tracer, Action request)
        {
            using (var scope = tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Request), message, sendSpanSetup))
            {
                try
                {
                    request();
                }
                catch (Exception ex)
                {
                    ex.SetupSpan(scope.Span);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task<T> RequestAsync<T>(object message, SpanSetup sendSpanSetup, ITracer tracer, Func<Task<T>> requestAsync)
        {
            using (var scope = tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Request), message, sendSpanSetup))
            {
                try
                {
                    return await requestAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ex.SetupSpan(scope.Span);
                    throw;
                }
            }
        }
    }
}
