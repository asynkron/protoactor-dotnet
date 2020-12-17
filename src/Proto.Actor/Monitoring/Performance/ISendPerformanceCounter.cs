namespace Proto.Monitoring.Performance
{
    public interface ISendPerformanceCounter
    {
        void Sent();
        void Faulted();
    }
}
