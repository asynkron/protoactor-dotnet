using System;
using System.Threading;

namespace Proto.Schedulers.SimpleScheduler
{
    public static class SimpleSchedulerExtensions
    {
        public static ISimpleScheduler ScheduleTellOnce(this ISenderContext senderContext, TimeSpan delay, PID target, object message) =>
        new SimpleScheduler(senderContext).ScheduleTellOnce(delay, target, message);
        public static ISimpleScheduler ScheduleTellRepeatedly(this ISenderContext senderContext, TimeSpan delay, TimeSpan interval, PID target, object message, out CancellationTokenSource cancellationTokenSource) =>
        new SimpleScheduler(senderContext).ScheduleTellRepeatedly(delay, interval, target, message, out cancellationTokenSource);
        public static ISimpleScheduler ScheduleRequestOnce(this ISenderContext senderContext, TimeSpan delay, PID sender, PID target, object message) =>
        new SimpleScheduler(senderContext).ScheduleRequestOnce(delay, sender, target, message);
        public static ISimpleScheduler ScheduleRequestRepeatedly(this ISenderContext senderContext, TimeSpan delay, TimeSpan interval, PID sender, PID target, object message, out CancellationTokenSource cancellationTokenSource) =>
        new SimpleScheduler(senderContext).ScheduleRequestRepeatedly(delay, interval, sender, target, message, out cancellationTokenSource);
    }
}
