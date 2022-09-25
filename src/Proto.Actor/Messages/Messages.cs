// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Marker interface for all built in message types
/// </summary>
public interface InfrastructureMessage
{
}

/// <summary>
///     Marker interface for all built in message types
/// </summary>
public interface IIgnoreDeadLetterLogging
{
}

/// <summary>
///     Notifies about actor termination, used together with <see cref="Terminated" />
/// </summary>
public sealed partial class Terminated : SystemMessage
{
}

/// <summary>
///     Notifies about actor restarting
/// </summary>
public sealed class Restarting : InfrastructureMessage
{
    public static readonly Restarting Instance = new();

    private Restarting()
    {
    }
}

/// <summary>
///     Diagnostic message to determine if an actor is responsive. Mostly used for debugging problems.
/// </summary>
public sealed partial class Touch : IAutoRespond, InfrastructureMessage
{
    public object GetAutoResponse(IContext context) =>
        new Touched
        {
            Who = context.Self
        };
}

/// <summary>
///     A user-level message that signals the actor to stop.
/// </summary>
public sealed partial class PoisonPill : IIgnoreDeadLetterLogging, InfrastructureMessage
{
    public static readonly PoisonPill Instance = new();
}

/// <summary>
///     Signals failure up the supervision hierarchy.
/// </summary>
public class Failure : SystemMessage
{
    public Failure(PID who, Exception reason, RestartStatistics crs, object? message)
    {
        Who = who;
        Reason = reason;
        RestartStatistics = crs;
        Message = message;
    }

    public Exception Reason { get; }
    public PID Who { get; }
    public RestartStatistics RestartStatistics { get; }
    public object? Message { get; }
}

/// <summary>
///     A message to subscribe to actor termination, used togeter with <see cref="Terminated" />
/// </summary>
public sealed partial class Watch : SystemMessage
{
    public Watch(PID watcher)
    {
        Watcher = watcher;
    }
}

/// <summary>
///     Unsubscribe from the termination notifications of the specified actor.
/// </summary>
public sealed partial class Unwatch : SystemMessage
{
    public Unwatch(PID watcher)
    {
        Watcher = watcher;
    }
}

/// <summary>
///     Signals the actor to restart
/// </summary>
public sealed class Restart : SystemMessage
{
    public Restart(Exception reason)
    {
        Reason = reason;
    }

    public Exception Reason { get; }
}

/// <summary>
///     A system-level message that signals the actor to stop.
/// </summary>
public partial class Stop : SystemMessage, IIgnoreDeadLetterLogging
{
    public static readonly Stop Instance = new();
}

/// <summary>
///     A message sent to the actor to indicate that it is about to stop. Handle this message in order to clean up.
/// </summary>
public sealed class Stopping : SystemMessage
{
    public static readonly Stopping Instance = new();

    private Stopping()
    {
    }
}

/// <summary>
///     A message sent to the actor to indicate that it has started. Handle this message to run additional initialization
///     logic.
/// </summary>
public sealed class Started : SystemMessage
{
    public static readonly Started Instance = new();

    private Started()
    {
    }
}

/// <summary>
///     A message sent to the actor to indicate that it has stopped.
/// </summary>
public sealed class Stopped : SystemMessage
{
    public static readonly Stopped Instance = new();

    private Stopped()
    {
    }
}

/// <summary>
///     When receive timeout expires, this message is sent to the actor to notify it. See
///     <see cref="IContext.SetReceiveTimeout" />
/// </summary>
public class ReceiveTimeout : SystemMessage
{
    public static readonly ReceiveTimeout Instance = new();

    private ReceiveTimeout()
    {
    }
}

/// <summary>
///     Messages marked with this interface will not reset the receive timeout timer. See
///     <see cref="IContext.SetReceiveTimeout" />
/// </summary>
public interface INotInfluenceReceiveTimeout
{
}

/// <summary>
///     Related to reentrancy, this message is sent to the actor after the awaited task is finished and actor can handle
///     the result. See <see cref="IContext.ReenterAfter{T}" />
/// </summary>
public class Continuation : SystemMessage
{
    public Continuation(Func<Task>? fun, object? message, IActor actor)
    {
        Action = fun ?? throw new ArgumentNullException(nameof(fun));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    public Func<Task> Action { get; }

    public object Message { get; }

    // This is used to track if actor was re-created or not.
    // If set to null, continuation always executes.
    public IActor Actor { get; }
}

/// <summary>
///     Request diagnostic information for the actor
/// </summary>
/// <param name="Result"></param>
public record ProcessDiagnosticsRequest(TaskCompletionSource<string> Result) : SystemMessage;

public static class Nothing
{
    public static readonly Empty Instance = new();
}