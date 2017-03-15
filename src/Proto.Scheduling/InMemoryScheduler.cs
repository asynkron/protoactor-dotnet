using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Scheduling
{
    public  class InMemoryScheduler : IScheduler
    {
        public void ScheduleMessage(IContext context, object message, DateTime timestamp)
        {
            AlarmClockActor.InstancePID.Tell(
                new AlarmClockActor.DelayMessage { Target = context.Self, Message = message, Timeout = timestamp }
                );
        }
    }
}
