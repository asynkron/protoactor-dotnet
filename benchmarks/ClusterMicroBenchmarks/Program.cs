using BenchmarkDotNet.Running;

namespace ClusterMicroBenchmarks
{
    class Program
    {
        static void Main() => BenchmarkRunner.Run<InProcessClusterRequestBenchmark>();
    }
}