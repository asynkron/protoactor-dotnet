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
        _logger.RootContextSending(GetLogLevel(message), message.GetMessageTypeName(), message, target);

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
        _logger.RootContextSendingRequest(GetLogLevel(message), message.GetMessageTypeName(), message, target);

        base.Request(target, message, sender);
    }

    public override void Stop(PID pid)
    {
        _logger.RootContextStopping(_logLevel, pid);

        base.Stop(pid);
    }

    public override void Poison(PID pid)
    {
        _logger.RootContextPoisoning(_logLevel, pid);
        base.Poison(pid);
    }

    public override async Task PoisonAsync(PID pid)
    {
        await base.PoisonAsync(pid);
        _logger.RootContextPoisoned(_logLevel, pid);
    }

    public override async Task StopAsync(PID pid)
    {
        await base.StopAsync(pid);
        _logger.RootContextStopped(_logLevel, pid);
    }

    public override PID SpawnNamed(Props props, string name, Action<IContext>? callback = null)
    {
        try
        {
            var pid = base.SpawnNamed(props, name, callback);
            _logger.RootContextSpawnedNamed(_logLevel, name, pid);

            return pid;
        }
        catch (Exception x)
        {
            _logger.RootContextFailedToSpawnNamed(_exceptionLogLevel, x, name);
            throw;
        }
    }

    public override void Request(PID target, object message)
    {
        _logger.RootContextSendingRequest(GetLogLevel(message), message.GetMessageTypeName(), message, target);

        base.Request(target, message);
    }

    public override async Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
    {
        var logLevel = GetLogLevel(message);
        var messageType = message.GetMessageTypeName();
        _logger.RootContextSendingRequestAsync(logLevel, messageType, message, target);
        try
        {
            var response = await base.RequestAsync<T>(target, message, cancellationToken);
            _logger.RootContextGotResponse(logLevel, response, messageType, message, target);

            return response;
        }
        catch (Exception x)
        {
            _logger.RootContextFailedSendingRequestAsync(_exceptionLogLevel, x, messageType, message, target);

            throw;
        }
    }
}