using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public class GrainCallOptions
    {
        public static readonly GrainCallOptions Default = new GrainCallOptions();

        public int RetryCount { get; set; } = 10;

        public Func<int, Task> RetryAction { get; set; } = ExponentialBackoff;

        public static async Task ExponentialBackoff(int i)
        {
            i++;
            await Task.Delay(i * i * 50);
        }
    }
}
