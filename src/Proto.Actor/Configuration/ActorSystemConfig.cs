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
    public class ActorSystemConfig
    {
        public static ActorSystemConfig Setup() => new ActorSystemConfig();
        
        public TimeSpan DeadLetterThrottleInterval { get; set; }
        public int DeadLetterThrottleCount { get; set; }

        public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval)
        {
            DeadLetterThrottleInterval = deadLetterThrottleInterval;
            return this;
        }
        
        public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount)
        {
            DeadLetterThrottleCount = deadLetterThrottleCount;
            return this;
        }
    }
}