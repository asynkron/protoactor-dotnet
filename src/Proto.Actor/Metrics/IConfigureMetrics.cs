namespace Proto.Metrics
{
    public interface IConfigureMetrics
    {
        ICountMetric     CreateCount(string name, string[] labelNames);
        IHistogramMetric CreateHistogram(string name, string[] labelNames);
        IGaugeMetric     CreateGauge(string name, string[] labelNames);
    }
}
