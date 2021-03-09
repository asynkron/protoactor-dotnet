namespace Proto.Metrics
{
    public interface IGaugeMetric
    {
        void Set(double value, params string[]? labels);
    }
}