// -----------------------------------------------------------------------
// <copyright file="ActorLoggingDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto
{
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
            props.WithContextDecorator(ctx => new ActorLoggingContext(ctx, logger, logLevel, infrastructureLogLevel, exceptionLogLevel));
    }

    public class ActorLoggingContext : ActorContextDecorator
    {
        private readonly ILogger _logger;
        private readonly LogLevel _logLevel;
        private readonly LogLevel _infrastructureLogLevel;
        private readonly LogLevel _exceptionLogLevel;

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

        public override async Task Receive(MessageEnvelope envelope)
        {
            var message = envelope.Message;

            var logLevel = GetLogLevel(message);

            if (_logger.IsEnabled(logLevel))
            {
                _logger.Log(logLevel, "Actor {Self} {ActorType} received message {MessageType}:{Message} from {Sender}", Self, ActorType, message.GetType().Name,
                    message, 
                    SenderOrNone(envelope)
                );
            }

            try
            {
                await base.Receive(envelope);

                if (_logger.IsEnabled(logLevel))
                {
                    _logger.Log(logLevel, "Actor {Self} {ActorType} completed message {MessageType}:{Message} from {Sender}", Self, ActorType,
                        message.GetType().Name,
                        message,
                        SenderOrNone(envelope)
                    );
                }
            }
            catch (Exception x)
            {
                if (_logger.IsEnabled(_exceptionLogLevel))
                {
                    _logger.Log(_exceptionLogLevel, x, "Actor {Self} {ActorType} failed during message {MessageType}:{Message} from {Sender}", Self, ActorType,
                        message.GetType().Name, message, 
                        SenderOrNone(envelope)
                    );
                }

                throw;
            }
        }

        private static string SenderOrNone(MessageEnvelope envelope) => envelope.Sender?.ToString() ?? "[No Sender]";

        private LogLevel GetLogLevel(object message)
        {
            var logLevel = message is InfrastructureMessage ? _infrastructureLogLevel : _logLevel;
            return logLevel;
        }

        public override PID SpawnNamed(Props props, string name)
        {
            try
            {
                var pid = base.SpawnNamed(props, name);

                if (_logger.IsEnabled(_logLevel))
                {
                    _logger.Log(_logLevel, "Actor {Self} {ActorType} Spawned child actor {Name} with PID {Pid}", Self, ActorType, name, pid
                    );
                }

                return pid;
            }
            catch (Exception x)
            {
                if (_logger.IsEnabled(_exceptionLogLevel))
                {
                    _logger.Log(_exceptionLogLevel, x, "Actor {Self} {ActorType} failed when spawning child actor {Name}", Self, ActorType, name);
                }

                throw;
            }
        }

        public override void Respond(object message)
        {
            var logLevel = GetLogLevel(message);

            if (_logger.IsEnabled(logLevel))
            {
                _logger.Log(logLevel, "Actor {Self} {ActorType} responded with {MessageType}:{Message} to {Sender}", Self, ActorType,
                    message.GetType().Name, message, Sender
                );
            }

            base.Respond(message);
        }

        private string ActorType => Actor?.GetType().Name ?? "None";
    }
}