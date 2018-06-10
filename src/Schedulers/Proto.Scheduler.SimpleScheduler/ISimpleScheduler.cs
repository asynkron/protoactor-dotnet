using System;
using System.Threading;

namespace Proto.Schedulers.SimpleScheduler
{
    public interface ISimpleScheduler
    {
        ISimpleScheduler ScheduleTellOnce(TimeSpan delay, PID target, object message);
        ISimpleScheduler ScheduleTellRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message, out CancellationTokenSource cancellationTokenSource);
        ISimpleScheduler ScheduleRequestOnce(TimeSpan delay, PID sender, PID target, object message);
        ISimpleScheduler ScheduleRequestRepeatedly(TimeSpan delay, TimeSpan interval, PID sender, PID target, object message, out CancellationTokenSource cancellationTokenSource);
    }
}
