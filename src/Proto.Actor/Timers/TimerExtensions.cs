namespace Proto.Timers
{
    public static class TimerExtensions
    {
        public static Scheduler Scheduler(this ISenderContext context) => new(context);
    }
}