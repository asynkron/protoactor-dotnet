using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTracing;
using OpenTracing.Propagation;

namespace Proto.OpenTracing
{
    internal class OpenTracingRootContextDecorator : RootContextDecorator
    {
        private readonly SpanSetup _sendSpanSetup;
        private readonly ITracer _tracer;

        public OpenTracingRootContextDecorator(IRootContext context, SpanSetup sendSpanSetup, ITracer tracer) :
            base(context)
        {
            _sendSpanSetup = (span, message) =>
            {
                ProtoTags.ActorType.Set(span, "<None>");
                sendSpanSetup(span, message);
            };

            _tracer = tracer;
        }

        public override void Send(PID target, object message)
            => OpenTracingMethodsDecorators.Send(target, message, _sendSpanSetup, _tracer,
                () => base.Send(target, message));

        public override void Request(PID target, object message)
            => OpenTracingMethodsDecorators.Request(target, message, _sendSpanSetup, _tracer,
                () => base.Request(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message, timeout)
            );

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message, cancellationToken)
            );
    }

    internal class OpenTracingActorContextDecorator : ActorContextDecorator
    {
        private readonly SpanSetup _receiveSpanSetup;
        private readonly SpanSetup _sendSpanSetup;
        private readonly ITracer _tracer;

        public OpenTracingActorContextDecorator(IContext context, SpanSetup sendSpanSetup, SpanSetup receiveSpanSetup,
            ITracer tracer) : base(context)
        {
            _sendSpanSetup = (span, message) =>
            {
                ProtoTags.ActorType.Set(span, context.Actor.GetType().Name);
                ProtoTags.SenderPID.Set(span, context.Self.ToString());
                sendSpanSetup(span, message);
            };
            _receiveSpanSetup = (span, message) =>
            {
                ProtoTags.ActorType.Set(span, context.Actor.GetType().Name);
                ProtoTags.TargetPID.Set(span, context.Self.ToString());
                receiveSpanSetup(span, message);
            };

            _tracer = tracer;
        }

        public override void Send(PID target, object message)
            => OpenTracingMethodsDecorators.Send(target, message, _sendSpanSetup, _tracer,
                () => base.Send(target, message));

        public override void Request(PID target, object message)
            => OpenTracingMethodsDecorators.Request(target, message, _sendSpanSetup, _tracer,
                () => base.Request(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message, timeout)
            );

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTracingMethodsDecorators.RequestAsync(target, message, _sendSpanSetup, _tracer,
                () => base.RequestAsync<T>(target, message, cancellationToken)
            );

        public override void Forward(PID target)
            => OpenTracingMethodsDecorators.Forward(target, base.Message, _sendSpanSetup, _tracer,
                () => base.Forward(target));

        public override Task Receive(MessageEnvelope envelope)
            => OpenTracingMethodsDecorators.Receive(envelope, _receiveSpanSetup, _tracer, () => base.Receive(envelope));
    }

    internal static class OpenTracingMethodsDecorators
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Send(PID target, object message, SpanSetup sendSpanSetup, ITracer tracer, Action send)
        {
            using IScope scope =
                tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Send), message, sendSpanSetup);

            try
            {
                ProtoTags.TargetPID.Set(scope.Span, target.ToString());
                send();
            }
            catch (Exception ex)
            {
                ex.SetupSpan(scope.Span);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Request(PID target, object message, SpanSetup sendSpanSetup, ITracer tracer,
            Action request)
        {
            using IScope scope =
                tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Request), message, sendSpanSetup);

            try
            {
                ProtoTags.TargetPID.Set(scope.Span, target.ToString());
                request();
            }
            catch (Exception ex)
            {
                ex.SetupSpan(scope.Span);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task<T> RequestAsync<T>(PID target, object message, SpanSetup sendSpanSetup,
            ITracer tracer, Func<Task<T>> requestAsync)
        {
            using IScope scope =
                tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Request), message, sendSpanSetup);

            try
            {
                ProtoTags.TargetPID.Set(scope.Span, target.ToString());
                return await requestAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.SetupSpan(scope.Span);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Forward(PID target, object message, SpanSetup sendSpanSetup, ITracer tracer,
            Action forward)
        {
            using IScope scope =
                tracer.BuildStartedScope(tracer.ActiveSpan?.Context, nameof(Forward), message, sendSpanSetup);

            try
            {
                ProtoTags.TargetPID.Set(scope.Span, target.ToString());
                forward();
            }
            catch (Exception ex)
            {
                ex.SetupSpan(scope.Span);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task Receive(MessageEnvelope envelope, SpanSetup receiveSpanSetup, ITracer tracer,
            Func<Task> receive)
        {
            object message = envelope.Message;

            ISpanContext parentSpanCtx = envelope.Header != null
                ? tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(envelope.Header.ToDictionary()))
                : null;

            using IScope scope = tracer.BuildStartedScope(parentSpanCtx, nameof(Receive), message, receiveSpanSetup);

            try
            {
                ISpan span = scope.Span;

                if (envelope.Sender != null)
                {
                    ProtoTags.SenderPID.Set(span, envelope.Sender.ToString());
                }

                receiveSpanSetup?.Invoke(span, message);

                await receive().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.SetupSpan(scope.Span);
                throw;
            }
        }
    }
}
