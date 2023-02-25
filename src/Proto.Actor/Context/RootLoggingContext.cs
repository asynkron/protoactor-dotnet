using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto;

public class RootLoggingContext : RootContextDecorator
{
    private readonly LogLevel _exceptionLogLevel;
    private readonly LogLevel _infrastructureLogLevel;
    private readonly ILogger _logger;
    private readonly LogLevel _logLevel;

    public RootLoggingContext(
        IRootContext context,
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
    
    public override void Send(PID target, object message)
    {
        var logLevel = GetLogLevel(message);

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "RootContext Sending {MessageType}:{Message} to {Target}", message.GetMessageTypeName(), message,
                target);
        }

        base.Send(target, message);
    }
    
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
    
    public override void Request(PID target, object message, PID? sender)
    {
        var logLevel = GetLogLevel(message);

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "Sending Request {MessageType}:{Message} to {Target}", message.GetMessageTypeName(),
                message, target);
        }

        base.Request(target, message, sender);
    }

    public override void Stop(PID pid)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "RootContext Stopping {Pid}", pid);
        }

        base.Stop(pid);
    }

    public override void Poison(PID pid)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "RootContext Poisoning {Pid}", pid);
        }

        base.Poison(pid);
    }

    public override async Task PoisonAsync(PID pid)
    {

        await base.PoisonAsync(pid).ConfigureAwait(false);
        
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "RootContext Poisoned {Pid}", pid);
        }
    }

    public override async Task StopAsync(PID pid)
    {
        await base.StopAsync(pid).ConfigureAwait(false);
        
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "RootContext Stopped {Pid}", pid);
        }
    }

    public override PID SpawnNamed(Props props, string name, Action<IContext>? callback = null)
    {
        try
        {
            var pid = base.SpawnNamed(props, name, callback);

            if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel, "RootContext Spawned child actor {Name} with PID {Pid}", name, pid);
            }

            return pid;
        }
        catch (Exception x)
        {
            if (_exceptionLogLevel != LogLevel.None && _logger.IsEnabled(_exceptionLogLevel))
            {
                _logger.Log(_exceptionLogLevel, x, "RootContext Failed when spawning child actor {Name}", name);
            }

            throw;
        }
    }

    public override void Request(PID target, object message)
    {
        var logLevel = GetLogLevel(message);

        if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "RootContext Sending Request {MessageType}:{Message} to {Target}", message.GetMessageTypeName(),
                message, target);
        }

        base.Request(target, message);
    }

    public override async Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
    {
        if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
        {
            _logger.Log(_logLevel, "RootContext Sending RequestAsync {MessageType}:{Message} to {Target}",
                message.GetMessageTypeName(), message, target
            );
        }

        try
        {
            var response = await base.RequestAsync<T>(target, message, cancellationToken).ConfigureAwait(false);

            if (_logLevel != LogLevel.None && _logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel,
                    "RootContext Got response {Response} to {MessageType}:{Message} from {Target}",
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
                    "RootContext Got exception waiting for RequestAsync response of {MessageType}:{Message} from {Target}",
                    message.GetMessageTypeName(), message, target
                );
            }

            throw;
        }
    }
}