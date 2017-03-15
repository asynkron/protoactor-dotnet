using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Scheduling
{
    public interface IScheduler
    {
        void ScheduleMessage(IContext context, object message, DateTime timestamp);
    }
}
