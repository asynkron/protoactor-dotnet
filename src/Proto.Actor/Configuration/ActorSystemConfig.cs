using System;

namespace Proto
{
    public class ActorSystemConfig
    {
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