using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Proto.Extensions;
using Proto.Mailbox;

namespace Proto.OpenTelemetry;

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
    public static PID SpawnNamed(string source, ActivitySetup sendActivitySetup, Func<PID> spawn, string name)
    {
        using var activity =
            OpenTelemetryHelpers.BuildStartedActivity(Activity.Current?.Context ?? default, source, nameof(IContext.SpawnNamed),
                "", sendActivitySetup);

        try
        {
            activity?.SetTag(ProtoTags.ActionType, nameof(IContext.SpawnNamed));
            
            var pid = spawn();
            activity?.SetTag(ProtoTags.TargetPID, pid.ToString());
            activity?.SetTag(ProtoTags.TargetName, name);
            return pid;
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
