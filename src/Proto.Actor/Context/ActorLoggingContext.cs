// -----------------------------------------------------------------------
// <copyright file="ActorLoggingDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto;

[PublicAPI]
public static class ActorLoggingContextExtensions
{
    public static Props WithLoggingContextDecorator(
        this Props props,
        ILogger logger,
        LogLevel logLevel = LogLevel.Debug,
        LogLevel infrastructureLogLevel = LogLevel.None,
        LogLevel exceptionLogLevel = LogLevel.Error
    ) =>
        props.WithContextDecorator(ctx =>
            new ActorLoggingContext(ctx, logger, logLevel, infrastructureLogLevel, exceptionLogLevel));
}

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

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "Actor {Self} {ActorType} received message {MessageType}:{Message} from {Sender}",
                Self, ActorType, message.GetType().Name,
                message,
                SenderOrNone(envelope)
            );
        }

        try
        {
            await base.Receive(envelope);

            if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
            {
                _logger.Log(logLevel,
                    "Actor {Self} {ActorType} completed message {MessageType}:{Message} from {Sender}", Self, ActorType,
                    message.GetType().Name,
                    message,
                    SenderOrNone(envelope)
                );
            }
        }
        catch (Exception x)
        {
            if (_exceptionLogLevel != LogLevel.None && _logger.IsEnabled(_exceptionLogLevel))
            {
                _logger.Log(_exceptionLogLevel, x,
                    "Actor {Self} {ActorType} failed during message {MessageType}:{Message} from {Sender}", Self,
                    ActorType,
                    message.GetType().Name, message,
                    SenderOrNone(envelope)
                );
            }

            throw;
        }
    }

    public override void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} ReenterAfter {Action}", Self, ActorType,
                action.Method.Name);
        }

        base.ReenterAfter(target, action);
    }

    public override void ReenterAfter(Task target, Action action)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} ReenterAfter {Action}", Self, ActorType,
                action.Method.Name);
        }

        base.ReenterAfter(target, action);
    }

    public override async Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} Sending RequestAsync {MessageType}:{Message} to {Target}",
                Self, ActorType,
                message.GetType().Name, message, target
            );
        }

        try
        {
            var response = await base.RequestAsync<T>(target, message, cancellationToken);

            if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel,
                    "Actor {Self} {ActorType} Got response {Response} to {MessageType}:{Message} from {Target}", Self,
                    ActorType,
                    response, message.GetType().Name, message, target
                );
            }

            return response;
        }
        catch (Exception x)
        {
            if (_exceptionLogLevel != LogLevel.None && _logger.IsEnabled(_exceptionLogLevel))
            {
                _logger.Log(_exceptionLogLevel, x,
                    "Actor {Self} {ActorType} Got exception waiting for RequestAsync response of {MessageType}:{Message} from {Target}",
                    Self,
                    ActorType,
                    message.GetType().Name, message, target
                );
            }

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

            if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel, "Actor {Self} {ActorType} Spawned child actor {Name} with PID {Pid}", Self,
                    ActorType, name, pid
                );
            }

            return pid;
        }
        catch (Exception x)
        {
            if (_exceptionLogLevel != LogLevel.None && _logger.IsEnabled(_exceptionLogLevel))
            {
                _logger.Log(_exceptionLogLevel, x, "Actor {Self} {ActorType} failed when spawning child actor {Name}",
                    Self, ActorType, name);
            }

            throw;
        }
    }

    public override void Respond(object message)
    {
        var logLevel = GetLogLevel(message);

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "Actor {Self} {ActorType} responded with {MessageType}:{Message} to {Sender}", Self,
                ActorType,
                message.GetType().Name, message, Sender
            );
        }

        base.Respond(message);
    }
}