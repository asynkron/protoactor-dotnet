// -----------------------------------------------------------------------
// <copyright file="ActorSystemConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Metrics;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.NoMetrics;

// -----------------------------------------------------------------------
//   <copyright file="ActorContext.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Proto
{
    [PublicAPI]
    public record ActorSystemConfig
    {
        public ILoggerFactory LoggerFactory { get; init; } = new NullLoggerFactory();
        public TimeSpan DeadLetterThrottleInterval { get; init; }

        public IMetricsProvider[] MetricsProviders { get; init; } = {new NoMetricsProvider()};
        public int DeadLetterThrottleCount { get; init; }

        public static ActorSystemConfig Setup() => new();

        public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval) =>
            this with {DeadLetterThrottleInterval = deadLetterThrottleInterval};

        public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount) =>
            this with {DeadLetterThrottleCount = deadLetterThrottleCount};
        
        public ActorSystemConfig WithLoggerFactory(ILoggerFactory loggerFactory) =>
            this with {LoggerFactory = loggerFactory};

        public ActorSystemConfig WithMetricsProviders(params IMetricsProvider[] providers) => this with {MetricsProviders = providers};
    }
}