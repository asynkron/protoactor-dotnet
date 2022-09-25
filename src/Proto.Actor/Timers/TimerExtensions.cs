namespace Proto.Timers;

public static class TimerExtensions
{
    /// <summary>
    ///     Gets a new scheduler that allows to schedule messages in the future
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static Scheduler Scheduler(this ISenderContext context) => new(context);
}