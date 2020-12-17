using System;

namespace Proto.Monitoring.Performance
{
    public interface IPerformanceCounter :
        IDisposable
    {
        void Increment();
        void IncrementBy(long val);
        void Set(long val);
    }
}
