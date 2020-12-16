using System;
using System.Threading;

namespace Proto.Timers
{
    public static class TimerExtensions
    {
        public static Scheduler Scheduler(this ISenderContext context) => new Scheduler(context);
    }
}
