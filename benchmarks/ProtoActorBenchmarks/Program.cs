using BenchmarkDotNet.Running;

namespace ProtoActorBenchmarks
{
    public class Program
    {
        public static void Main(string[] args) => BenchmarkRunner.Run<ShortBenchmark>();
    }
}