using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Proto.Extensions;
using Proto.Mailbox;

namespace Proto.OpenTelemetry;

internal class OpenTelemetryRootContextDecorator : RootContextDecorator
{
    private readonly ActivitySetup _sendActivitySetup;

    public OpenTelemetryRootContextDecorator(IRootContext context, ActivitySetup sendActivitySetup) : base(context)
    {
        _sendActivitySetup = (activity, message)
            =>
        {
            activity?.SetTag(ProtoTags.ActorType, "<None>");

            if (activity != null)
            {
                sendActivitySetup(activity, message);
            }
        };
    }

    private static string Source => "Root";

    public override void Send(PID target, object message) =>
        OpenTelemetryMethodsDecorators.Send(Source, target, message, _sendActivitySetup,
            () => base.Send(target, message));

    public override void Request(PID target, object message) =>
        OpenTelemetryMethodsDecorators.Request(Source, target, message, _sendActivitySetup,
            () => base.Request(target, message));

    public override void Request(PID target, object message, PID? sender) =>
        OpenTelemetryMethodsDecorators.Request(Source, target, message, sender, _sendActivitySetup,
            () => base.Request(target, message, sender));

    public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
        OpenTelemetryMethodsDecorators.RequestAsync(Source, target, message, _sendActivitySetup,
            () => base.RequestAsync<T>(target, message, cancellationToken)
        );
}

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
}

internal static class OpenTelemetryMethodsDecorators
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Send(string source, PID target, object message, ActivitySetup sendActivitySetup, Action send)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(Send),
                message, sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Send));
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
    internal static void Request(string source, PID target, object message, ActivitySetup sendActivitySetup,
        Action request)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(Request),
                message, sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Request));
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
    internal static void Request(string source, PID target, object message, PID? sender,
        ActivitySetup sendActivitySetup, Action request)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(Request),
                message, sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Request));
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
    internal static async Task<T> RequestAsync<T>(string source, PID target, object message,
        ActivitySetup sendActivitySetup, Func<Task<T>> requestAsync)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(RequestAsync),
                message, sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(RequestAsync));
            activity?.SetTag(ProtoTags.TargetPID, target.ToString());

            var res = await requestAsync().ConfigureAwait(false);
            activity?.SetTag(ProtoTags.ResponseMessageType, res.GetMessageTypeName());
            return res;
        }
        catch (TimeoutException ex)
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(RequestAsync));
            activity?.RecordException(ex);
            activity?.SetStatus(Status.Error);
            activity?.AddEvent(new ActivityEvent("Request Timeout"));

            throw;
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(Status.Error);

            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Forward(string source, PID target, object message, ActivitySetup sendActivitySetup,
        Action forward)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(Forward),
                message, sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Forward));
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
    internal static void Respond(object message,
        Action respond)
    {
        var activity = Activity.Current;

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Respond));
            activity?.SetTag(ProtoTags.ResponseMessageType, message.GetMessageTypeName());
            respond();
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(Status.Error);

            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static async Task Receive(string source, MessageEnvelope envelope, ActivitySetup receiveActivitySetup,
        Func<Task> receive)
    {
        var message = envelope.Message;

        if (message is SystemMessage)
        {
            await receive().ConfigureAwait(false);

            return;
        }

        var propagationContext = envelope.Header.ExtractPropagationContext();

        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(propagationContext.ActivityContext, source, nameof(Receive),
                message, receiveActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(Receive));
            if (envelope.Sender != null)
            {
                activity?.SetTag(ProtoTags.SenderPID, envelope.Sender.ToString());
            }

            if (activity != null)
            {
                receiveActivitySetup?.Invoke(activity, message);
            }

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
