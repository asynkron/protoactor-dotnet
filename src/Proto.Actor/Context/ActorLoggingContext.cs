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

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "Actor {Self} {ActorType} received message {MessageType}:{Message} from {Sender}",
                Self, ActorType, message.GetMessageTypeName(),
                message,
                SenderOrNone(envelope)
            );
        }

        try
        {
            await base.Receive(envelope).ConfigureAwait(false);

            if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
            {
                _logger.Log(logLevel,
                    "Actor {Self} {ActorType} completed message {MessageType}:{Message} from {Sender}", Self, ActorType,
                    message.GetMessageTypeName(),
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
                    message.GetMessageTypeName(), message,
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
                message.GetMessageTypeName(), message, target
            );
        }

        try
        {
            var response = await base.RequestAsync<T>(target, message, cancellationToken).ConfigureAwait(false);

            if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel,
                    "Actor {Self} {ActorType} Got response {Response} to {MessageType}:{Message} from {Target}", Self,
                    ActorType,
                    response, message.GetMessageTypeName(), message, target
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
                    message.GetMessageTypeName(), message, target
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
                message.GetMessageTypeName(), message, Sender
            );
        }

        base.Respond(message);
    }

    public override void Forward(PID target)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} forwarded message to {Target}", Self, ActorType, target);
        }

        base.Forward(target);
    }

    public override void Request(PID target, object message, PID? sender)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} Sending Request {MessageType}:{Message} to {Target}",
                Self, ActorType,
                message.GetMessageTypeName(), message, target
            );
        }

        base.Request(target, message, sender);
    }

    public override void Send(PID target, object message)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} Sending {MessageType}:{Message} to {Target}", Self,
                ActorType,
                message.GetMessageTypeName(), message, target
            );
        }

        base.Send(target, message);
    }

    public override void Unwatch(PID pid)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} Unwatching {Pid}", Self, ActorType, pid);
        }

        base.Unwatch(pid);
    }
    
    public override void Watch(PID pid)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "Actor {Self} {ActorType} Watching {Pid}", Self, ActorType, pid);
        }

        base.Watch(pid);
    }
}