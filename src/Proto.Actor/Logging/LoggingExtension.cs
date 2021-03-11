// -----------------------------------------------------------------------
// <copyright file="LoggingExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Microsoft.Extensions.Logging;
using Proto.Context;
using Proto.Extensions;

namespace Proto.Logging
{
    public class LogExtension : IActorSystemExtension<LogExtension>
    {
        public LogExtension(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            ActorContextLogger = loggerFactory.CreateLogger<ActorContext>();
        }

        public ILoggerFactory LoggerFactory { get; } 

        internal readonly ILogger ActorContextLogger;
    }
    
    public static class LogExtensionExtensions
    {
        public static ILoggerFactory LoggerFactory(this ActorSystem actorSystem) => 
            actorSystem.Extensions.Get<LogExtension>()!.LoggerFactory;
    }
}