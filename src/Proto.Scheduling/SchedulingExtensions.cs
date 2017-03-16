using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Scheduling
{
    public static class SchedulingExtensions
    {
        public static Sender SchedulerMiddleware(Sender next, IScheduler scheduler)
        {
            return (context, target, message) =>
            {
                if (message.Message is DelayMessage dm)
                    return scheduler.ScheduleMessage(dm);
                else
                    return next(context, target, message);
            };
        }


        public static Props WithScheduler(this Props props, IScheduler scheduler)
            => props.WithSenderMiddleware(s => SchedulerMiddleware(s, scheduler));


        public static void Schedule(this IContext context, object message, double seconds)
            => context.Schedule(message, DateTime.UtcNow.AddSeconds(seconds));

        public static void Schedule(this IContext context, object message, TimeSpan delay)
            => context.Schedule(message, DateTime.UtcNow + delay);

        public static void Schedule(this IContext context, object message, DateTime timestamp)
            => context.Tell(context.Self, new DelayMessage { Target = context.Self, Message = message, Timeout = timestamp });
    }
}
