using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Scheduling
{
    public static class SchedulingExtensions
    {
        public static Sender SchedulerMiddleware(Sender next, IScheduler transientScheduler, IScheduler persistentScheduler)
        {
            return (context, target, message) =>
            {
                if (message.Message is TransientDelayMessage tdm) return transientScheduler.ScheduleMessage(tdm);
                if (message.Message is PersistentDelayMessage pdm) return persistentScheduler.ScheduleMessage(pdm);
                else return next(context, target, message);
            };
        }


        public static Props WithScheduler(this Props props, IScheduler onlyScheduler)
            => props.WithSenderMiddleware(s => SchedulerMiddleware(s, onlyScheduler, onlyScheduler));
        public static Props WithScheduler(this Props props, IScheduler transientScheduler, IScheduler persistentScheduler)
            => props.WithSenderMiddleware(s => SchedulerMiddleware(s, transientScheduler, persistentScheduler));


        public static void ScheduleTransient(this IContext context, object message, double seconds)
            => context.ScheduleTransient(message, DateTime.UtcNow.AddSeconds(seconds));
        public static void ScheduleTransient(this IContext context, object message, TimeSpan delay)
            => context.ScheduleTransient(message, DateTime.UtcNow + delay);
        public static void ScheduleTransient(this IContext context, object message, DateTime timestamp)
            => context.Tell(context.Self, new TransientDelayMessage { Target = context.Self, Message = message, Timeout = timestamp });


        public static void SchedulePersistent(this IContext context, object message, double seconds)
          => context.SchedulePersistent(message, DateTime.UtcNow.AddSeconds(seconds));
        public static void SchedulePersistent(this IContext context, object message, TimeSpan delay)
            => context.SchedulePersistent(message, DateTime.UtcNow + delay);
        public static void SchedulePersistent(this IContext context, object message, DateTime timestamp)
            => context.Tell(context.Self, new PersistentDelayMessage { Target = context.Self, Message = message, Timeout = timestamp });
    }
}
