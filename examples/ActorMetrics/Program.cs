using System;
using System.Threading.Tasks;
using Proto;
using Ubiquitous.Metrics.Labels;
using Ubiquitous.Metrics.Prometheus;

namespace ActorMetrics
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());
            var system = new ActorSystem(config);
            var props = Props.FromFunc(ctx => {
                    Console.WriteLine(ctx.Message);
                    return Task.CompletedTask;
                }
            );

            var pid = system.Root.Spawn(props);
            system.Root.Send(pid, "hello");

            Console.ReadLine();
        }
    }
}