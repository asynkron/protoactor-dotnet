namespace Proto.Metrics
{
    public interface ICountMetric
    {
        void Inc(int count = 1, params string[]? labels);
    }
}
