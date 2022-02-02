using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Proto.Mailbox;

namespace Proto.OpenTelemetry
{
    class OpenTelemetryRootContextDecorator : RootContextDecorator
    {
        private readonly ActivitySetup _sendActivitySetup;

        public OpenTelemetryRootContextDecorator(IRootContext context, ActivitySetup sendActivitySetup) : base(context)
            => _sendActivitySetup = (activity, message)
                => {
                activity?.SetTag(ProtoTags.ActorType, "<None>");
                sendActivitySetup(activity, message);
            };

        public override void Send(PID target, object message)
            => OpenTelemetryMethodsDecorators.Send(target, message, _sendActivitySetup, () => base.Send(target, message));

        public override void Request(PID target, object message)
            => OpenTelemetryMethodsDecorators.Request(target, message, _sendActivitySetup, () => base.Request(target, message));

        public override void Request(PID target, object message, PID? sender)
            => OpenTelemetryMethodsDecorators.Request(target, message, sender, _sendActivitySetup, () => base.Request(target, message, sender));

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTelemetryMethodsDecorators.RequestAsync(target, message, _sendActivitySetup,
                () => base.RequestAsync<T>(target, message, cancellationToken)
            );
    }

    class OpenTelemetryActorContextDecorator : ActorContextDecorator
    {
        private readonly ActivitySetup _receiveActivitySetup;
        private readonly ActivitySetup _sendActivitySetup;

        public OpenTelemetryActorContextDecorator(
            IContext context,
            ActivitySetup sendActivitySetup,
            ActivitySetup receiveActivitySetup
        ) : base(context)
        {
            var actorType = context.Actor.GetType().Name;
            var self = context.Self.ToString();
            _sendActivitySetup = (activity, message) => {
                activity?.SetTag(ProtoTags.ActorType, actorType);
                activity?.SetTag(ProtoTags.ActorPID, self);
                activity?.SetTag(ProtoTags.SenderPID, self);
                sendActivitySetup(activity, message);
            };
            _receiveActivitySetup = (activity, message) => {
                activity?.SetTag(ProtoTags.ActorType, actorType);
                activity?.SetTag(ProtoTags.ActorPID, self);
                activity?.SetTag(ProtoTags.TargetPID, self);
                receiveActivitySetup(activity, message);
            };
        }

        public override void Send(PID target, object message)
            => OpenTelemetryMethodsDecorators.Send(target, message, _sendActivitySetup, () => base.Send(target, message));

        public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => OpenTelemetryMethodsDecorators.RequestAsync(target, message, _sendActivitySetup,
                () => base.RequestAsync<T>(target, message, cancellationToken)
            );

        public override void Request(PID target, object message, PID? sender)
            => OpenTelemetryMethodsDecorators.Request(target, message, sender, _sendActivitySetup, () => base.Request(target, message, sender));

        public override void Forward(PID target)
            => OpenTelemetryMethodsDecorators.Forward(target, base.Message!, _sendActivitySetup, () => base.Forward(target));

        public override Task Receive(MessageEnvelope envelope)
            => OpenTelemetryMethodsDecorators.Receive(envelope, _receiveActivitySetup, () => base.Receive(envelope));
    }

    static class OpenTelemetryMethodsDecorators
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Send(PID target, object message, ActivitySetup sendActivitySetup, Action send)
        {
            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, nameof(Send), message, sendActivitySetup);

            try
            {
                activity?.SetTag(ProtoTags.TargetPID, target.ToString());
                send();
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Request(PID target, object message, ActivitySetup sendActivitySetup, Action request)
        {
            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, nameof(Request), message, sendActivitySetup);

            try
            {
                activity?.SetTag(ProtoTags.TargetPID, target.ToString());
                request();
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Request(PID target, object message, PID? sender, ActivitySetup sendActivitySetup, Action request)
        {
            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, nameof(Request), message, sendActivitySetup);

            try
            {
                activity?.SetTag(ProtoTags.TargetPID, target.ToString());

                if (sender is not null)
                {
                    activity?.SetTag(ProtoTags.SenderPID, sender.ToString());
                }

                request();
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task<T> RequestAsync<T>(PID target, object message, ActivitySetup sendActivitySetup, Func<Task<T>> requestAsync)
        {
            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, nameof(Request), message, sendActivitySetup);

            try
            {
                activity?.SetTag(ProtoTags.TargetPID, target.ToString());
                return await requestAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Forward(PID target, object message, ActivitySetup sendActivitySetup, Action forward)
        {
            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, nameof(Forward), message, sendActivitySetup);

            try
            {
                activity?.SetTag(ProtoTags.TargetPID, target.ToString());
                forward();
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task Receive(MessageEnvelope envelope, ActivitySetup receiveActivitySetup, Func<Task> receive)
        {
            var message = envelope.Message;
            
            if (message is SystemMessage)
            {
                await receive().ConfigureAwait(false);
                return;
            }

            var propagationContext = envelope.Header.ExtractPropagationContext();

            using var activity =
                OpenTelemetryHelpers.BuildStartedActivity(propagationContext.ActivityContext, nameof(Receive), message, receiveActivitySetup);

            try
            {
                if (envelope.Sender != null) activity?.SetTag(ProtoTags.SenderPID, envelope.Sender.ToString());

                receiveActivitySetup?.Invoke(activity, message);

                await receive().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(Status.Error);
                throw;
            }
        }
    }
}