using System;
using System.Threading.Tasks;
using Prometheus;
using Proto;
using Ubiquitous.Metrics.Prometheus;

namespace ActorMetrics
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new MetricServer("localhost", 1234);
            server.Start();
            
            var config = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());
            var system = new ActorSystem(config);
            var props = Props.FromProducer(() => new MyActor());

            var pid = system.Root.Spawn(props);
            system.Root.Send(pid,new MyMessage("Asynkron"));

            Console.ReadLine();
        }
    }
    
    public record MyMessage(string Name);

    public class MyActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is MyMessage m)
            {
                Console.WriteLine(m.Name);
            }

            return Task.CompletedTask;
        }
    }
}