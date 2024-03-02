using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.OpenTelemetry;

internal class OpenTelemetryActorContextDecorator : ActorContextDecorator
{
    private readonly ActivitySetup _receiveActivitySetup;
    private readonly ActivitySetup _sendActivitySetup;

    public OpenTelemetryActorContextDecorator(
        IContext context,
        ActivitySetup sendActivitySetup,
        ActivitySetup receiveActivitySetup
    ) : base(context)
    {
        var actorType = Source;
        var self = context.Self.ToString();

        _sendActivitySetup = (activity, message) =>
        {
            activity?.SetTag(ProtoTags.ActorType, actorType);
            activity?.SetTag(ProtoTags.ActorPID, self);
            activity?.SetTag(ProtoTags.SenderPID, self);

            if (activity != null)
            {
                sendActivitySetup(activity, message);
            }
        };

        _receiveActivitySetup = (activity, message) =>
        {
            activity?.SetTag(ProtoTags.ActorType, actorType);
            activity?.SetTag(ProtoTags.ActorPID, self);
            activity?.SetTag(ProtoTags.TargetPID, self);

            if (activity != null)
            {
                receiveActivitySetup(activity, message);
            }
        };
    }

    private string Source => base.Actor?.GetType().Name ?? "<None>";

    public override void Send(PID target, object message) =>
        OpenTelemetryMethodsDecorators.Send(Source, target, message, _sendActivitySetup,
            () => base.Send(target, message));

    public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
        OpenTelemetryMethodsDecorators.RequestAsync(Source, target, message, _sendActivitySetup,
            () => base.RequestAsync<T>(target, message, cancellationToken)
        );

    public override void Request(PID target, object message, PID? sender) =>
        OpenTelemetryMethodsDecorators.Request(Source, target, message, sender, _sendActivitySetup,
            () => base.Request(target, message, sender));

    public override void Forward(PID target) =>
        OpenTelemetryMethodsDecorators.Forward(Source, target, base.Message!, _sendActivitySetup,
            () => base.Forward(target));

    public override Task Receive(MessageEnvelope envelope) =>
        OpenTelemetryMethodsDecorators.Receive(Source, envelope, _receiveActivitySetup,
            () => base.Receive(envelope));

    public override void Respond(object message)=>
        OpenTelemetryMethodsDecorators.Respond(message,
            () => base.Respond(message));

    public override void ReenterAfter(Task target, Action action)
    {
        var current = Activity.Current?.Context ?? default;
        var message = base.Message!;
        var a2 = () =>
        {
            using var x = OpenTelemetryHelpers.BuildStartedActivity(current, Source, nameof(ReenterAfter), message,
                _sendActivitySetup);
            x?.SetTag(ProtoTags.ActionType, nameof(ReenterAfter));
            action();
        };
        base.ReenterAfter(target, a2);
    }

    public override void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
    {
        var current = Activity.Current?.Context ?? default;
        var message = base.Message!;
        Func<Task<T>, Task> a2 = async t =>
        {
            using var x = OpenTelemetryHelpers.BuildStartedActivity(current, Source, nameof(ReenterAfter), message,
                _sendActivitySetup);
            x?.SetTag(ProtoTags.ActionType, nameof(ReenterAfter));
            await action(t).ConfigureAwait(false);
        };
        base.ReenterAfter(target, a2);
    }

    public override PID SpawnNamed(Props props, string name, Action<IContext>? callback = null) => OpenTelemetryMethodsDecorators.SpawnNamed(Source,_sendActivitySetup, () => base.SpawnNamed(props, name, callback),name);
}