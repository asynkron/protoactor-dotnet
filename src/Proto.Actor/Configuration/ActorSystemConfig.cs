using System;
using JetBrains.Annotations;

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
        public static ActorSystemConfig Setup() => new ActorSystemConfig();
        
        public TimeSpan DeadLetterThrottleInterval { get; init; }
        public int DeadLetterThrottleCount { get; init; }

        public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval) => 
            this with { DeadLetterThrottleInterval = deadLetterThrottleInterval};

        public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount) => 
            this with {DeadLetterThrottleCount = deadLetterThrottleCount};
    }
}