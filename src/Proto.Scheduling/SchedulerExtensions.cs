using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Scheduling
{
    public static class SchedulerExtensions
    {
        public static void ScheduleMessage(this IScheduler scheduler, IContext context, object message, double seconds)
            => scheduler.ScheduleMessage(context, message, DateTime.UtcNow.AddSeconds(seconds));

        public static void ScheduleMessage(this IScheduler scheduler, IContext context, object message, TimeSpan delay)
            => scheduler.ScheduleMessage(context, message, DateTime.UtcNow + delay);
    }
}
