// -----------------------------------------------------------------------
// <copyright file="ActorLoggingDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto;

/// <summary>
///     A decorator for <see cref="Proto.Context.ActorContext" /> that logs events related to message delivery to the
///     actor.
/// </summary>
public class ActorLoggingContext : ActorContextDecorator
{
    private readonly LogLevel _exceptionLogLevel;
    private readonly LogLevel _infrastructureLogLevel;
    private readonly ILogger _logger;
    private readonly LogLevel _logLevel;

    public ActorLoggingContext(
        IContext context,
        ILogger logger,
        LogLevel logLevel = LogLevel.Debug,
        LogLevel infrastructureLogLevel = LogLevel.None,
        LogLevel exceptionLogLevel = LogLevel.Error
    ) : base(context)
    {
        _logger = logger;
        _logLevel = logLevel;
        _infrastructureLogLevel = infrastructureLogLevel;
        _exceptionLogLevel = exceptionLogLevel;
    }

    private string ActorType => Actor?.GetType().Name ?? "None";

    public override async Task Receive(MessageEnvelope envelope)
    {
        var message = envelope.Message;
        var logLevel = GetLogLevel(message);
        var messageType = message.GetMessageTypeName();
        var sender = SenderOrNone(envelope);
        _logger.ActorReceivedMessage(logLevel, Self, ActorType, messageType, message, sender);
        try
        {
            await base.Receive(envelope);
            _logger.ActorCompletedMessage(logLevel, Self, ActorType, messageType, message, sender);
        }
        catch (Exception x)
        {
            _logger.ActorFailedToCompleteMessage(_exceptionLogLevel, x, Self, ActorType, messageType, message, sender);

            throw;
        }
    }

    public override void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
    {
        _logger.ActorReenterAfter(_logLevel, Self, ActorType, action.Method.Name);

        base.ReenterAfter(target, action);
    }

    public override void ReenterAfter(Task target, Action action)
    {
        _logger.ActorReenterAfter(_logLevel, Self, ActorType, action.Method.Name);

        base.ReenterAfter(target, action);
    }

    public override async Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
    {
        var messageType = message.GetMessageTypeName();
        var logLevel = GetLogLevel(message);
        _logger.ActorSendingRequestAsync(logLevel, Self, ActorType, messageType, message, target);
        try
        {
            var response = await base.RequestAsync<T>(target, message, cancellationToken);
            _logger.ActorGotResponse(logLevel, Self, ActorType, response, messageType, message, target);

            return response;
        }
        catch (Exception x)
        {
            _logger.ActorFailedWaitingForRequestAsync(_exceptionLogLevel, x, Self, ActorType, messageType, message, target);
            throw;
        }
    }

    private static string SenderOrNone(MessageEnvelope envelope) => envelope.Sender?.ToString() ?? "[No Sender]";

    private LogLevel GetLogLevel(object message)
    {
        // Don't log certain messages, as the Partition*Actor ends up spamming logs without this.
        if (message is Terminated or Touch)
        {
            return LogLevel.None;
        }

        var logLevel = message is InfrastructureMessage ? _infrastructureLogLevel : _logLevel;

        return logLevel;
    }

    public override PID SpawnNamed(Props props, string name, Action<IContext>? callback = null)
    {
        try
        {
            var pid = base.SpawnNamed(props, name, callback);
            _logger.ActorSpawnedNamed(_logLevel, Self, ActorType, name, pid);

            return pid;
        }
        catch (Exception x)
        {
            _logger.ActorFailedSpawningChildActor(_exceptionLogLevel, x, Self, ActorType, name);

            throw;
        }
    }

    public override void Respond(object message)
    {
        var logLevel = GetLogLevel(message);
        _logger.ActorResponded(logLevel, Self, ActorType, message.GetMessageTypeName(), message, Sender);

        base.Respond(message);
    }

    public override void Forward(PID target)
    {
        _logger.ActorForwarded(_logLevel, Self, ActorType, target);

        base.Forward(target);
    }

    public override void Request(PID target, object message, PID? sender)
    {
        _logger.ActorSendingRequest(GetLogLevel(message), Self, ActorType, message.GetMessageTypeName(), message, target);

        base.Request(target, message, sender);
    }

    public override void Send(PID target, object message)
    {
        _logger.ActorSending(GetLogLevel(message), Self, ActorType, message.GetMessageTypeName(), message, target);

        base.Send(target, message);
    }

    public override void Unwatch(PID pid)
    {
        _logger.ActorUnwatching(_logLevel, Self, ActorType, pid);

        base.Unwatch(pid);
    }

    public override void Watch(PID pid)
    {
        _logger.ActorWatching(_logLevel, Self, ActorType, pid);

        base.Watch(pid);
    }
}