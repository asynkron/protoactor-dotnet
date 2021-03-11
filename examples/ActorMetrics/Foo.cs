// -----------------------------------------------------------------------
// <copyright file="Foo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto;
using Ubiquitous.Metrics.Prometheus;

namespace ActorMetrics
{
    public class Foo
    {
        public static void Run()
        {
            var config = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());
            var system = new ActorSystem(config);
            var props = Props.FromProducer(() => new MyActor());

            var pid = system.Root.Spawn(props);
            system.Root.Send(pid, new MyMessage("Asynkron"));
          //  system.Root.Poison(pid);

            _ = Task.Run(async () => {

                    var r = new Random();
                    while (true)
                    {
                        await Task.Delay(r.Next(5,900));
                        system.Root.Send(pid, new MyMessage("Asynkron"));


                    }

                }
            );

        }
    }
    
    public record MyMessage(string Name);

    public class MyActor : IActor
    {
        private Random r = new();
        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is MyMessage m)
            {
                await Task.Delay(r.Next(0, 1000));
                Console.WriteLine(m.Name);
            }
        }
    }
}