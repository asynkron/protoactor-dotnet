// -----------------------------------------------------------------------
// <copyright file="ActorLoggingDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
        public static Props WithLoggingContextDecorator(this Props props, ILogger logger, bool ignoreInfrastructureMessages = true) => 
            props.WithContextDecorator(ctx => new ActorLoggingContext(ctx, logger, ignoreInfrastructureMessages));
    }

    public class ActorLoggingContext : ActorContextDecorator
    {
        private readonly ILogger _logger;
        private readonly bool _ignoreInfrastructureMessages;

        public ActorLoggingContext(IContext context, ILogger logger, bool ignoreInfrastructureMessages = true) : base(context)
        {
            _logger = logger;
            _ignoreInfrastructureMessages = ignoreInfrastructureMessages;
        }

        public override async Task Receive(MessageEnvelope envelope)
        {
            var message = envelope.Message;

            //ignore any built in messages
            if (_ignoreInfrastructureMessages && message is InfrastructureMessage)
            {
                await base.Receive(envelope);
                return;
            }

            _logger.LogInformation("Actor {Self} {ActorType} received message {Message}", Self, ActorType, message);

            try
            {
                await base.Receive(envelope);
                _logger.LogInformation("Actor {Self} {ActorType} completed message {Message}", Self, ActorType, message);
            }
            catch (Exception x)
            {
                _logger.LogInformation(x, "Actor {Self} {ActorType} failed during message {Message}", Self, ActorType, message);
                
                throw;
            }
        }

        public override PID SpawnNamed(Props props, string name)
        {
            try
            {
                var pid = base.SpawnNamed(props, name);
                _logger.LogInformation("Actor {Self} {ActorType} Spawned child actor {Name} with PID {Pid}", Self, ActorType, name, pid);
                return pid;
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Actor {Self} {ActorType} failed when spawning child actor {Name}", Self, ActorType, name);
                throw;
            }
        }

        public override void Respond(object message)
        {
            //ignore any built in messages
            if (_ignoreInfrastructureMessages && message is InfrastructureMessage)
            {
                base.Respond(message);
                return;
            }

            _logger.LogInformation("Actor {Self} {ActorType} responded with {Message} to {Sender}", Self, ActorType, message, Sender);
            base.Respond(message);
        }

        private string ActorType => Actor?.GetType().Name;

    }
}