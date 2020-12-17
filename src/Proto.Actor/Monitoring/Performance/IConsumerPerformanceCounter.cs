using System;

namespace Proto.Monitoring.Performance
{
    public interface IConsumerPerformanceCounter
    {
        void Consumed(TimeSpan duration);
        void Faulted();
    }
}
