using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public static class AlarmClockExtensions
    {
        public static void SelfDelayMessage(this IContext context, object message, double seconds)
            => SelfDelayMessage(context, message, DateTime.UtcNow.AddSeconds(seconds));

        public static void SelfDelayMessage(this IContext context, object message, TimeSpan delay)
            => SelfDelayMessage(context, message, DateTime.UtcNow + delay);

        public static void SelfDelayMessage(this IContext context, object message, DateTime timeout)
        {
            AlarmClock.InstancePID.Tell(
                new AlarmClock.DelayMessage { Target = context.Self, Message = message, Timeout = timeout }
                );
        }
    }
}
