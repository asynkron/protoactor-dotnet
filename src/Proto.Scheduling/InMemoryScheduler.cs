using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Scheduling
{
    public class InMemoryScheduler : IScheduler
    {
        public Task ScheduleMessage(DelayMessage delayMessage)
        {
            AlarmClockActor.InstancePID.Tell(delayMessage);
            return Actor.Done;
        }
    }
}
