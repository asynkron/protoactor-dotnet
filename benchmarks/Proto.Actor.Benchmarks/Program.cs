using BenchmarkDotNet.Running;

namespace Proto.Actor.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) => BenchmarkRunner.Run<ShortBenchmark>();
    }
}