using System;

namespace Proto.Timers;

public static class TimerExtensions
{
    /// <summary>
    ///     Gets a new scheduler that allows to schedule messages in the future
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static Scheduler Scheduler(this ISenderContext context) => new(context);

    /// <summary>
    ///     Gets a new scheduler that allows to schedule messages in the future
    /// </summary>
    /// <param name="context">Context to send the scheduled message through</param>
    /// <param name="timeProvider">TimeProvider to use for scheduling (FakeTimeProvider can be used for testing)</param>
    /// <returns></returns>
    public static Scheduler Scheduler(this ISenderContext context, TimeProvider timeProvider) => new(context, timeProvider);
}
