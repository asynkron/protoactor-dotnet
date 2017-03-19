using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Scheduling
{
    public interface IScheduler
    {
        Task ScheduleMessage(DelayMessage delayMessage);
    }
}
