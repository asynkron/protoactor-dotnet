using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;

namespace ClusterExperiment1
{
    public static class Program
    {
        public static async Task Main()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));
            var logger = Log.CreateLogger(nameof(Program));

            var system1 = new ActorSystem();
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var serialization1 = new Serialization();
            serialization1.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster1 = new Cluster(system1, serialization1);
            await cluster1.StartAsync(new ClusterConfig("mycluster", "127.0.0.1", 8090, consul1).WithPidCache(false));
            SpawnMember(8091);
            SpawnMember(8092);
            SpawnMember(8093);

            await Task.Delay(1000);

            // Task.Run(async () =>
            //     {
            //         for (int i = 0; i < 3; i++)
            //         {
            //             logger.LogInformation(">>>>>>>>>>> " + i);
            //             SpawnMember(8094);
            //
            //             await Task.Delay(3000);
            //         }
            //     }
            // );

            Task.Run(async () =>
                {
                    var rnd = new Random();
                    while (true)
                    {
                        var id = "myactor" + rnd.Next(0, 100);
                        //    Console.WriteLine($"Sending request {id}");
                        var res = await cluster1.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                            CancellationToken.None
                        );

                        if (res == null)
                        {
                            logger.LogError("Got void response");
                        }
                        else
                        {
                            //Console.Write(".");
                            //      Console.WriteLine("Got response");
                        }

                        //    await Task.Delay(10);
                    }
                }
            );

            int port = 8094;
            
            while (true)
            {
                Console.ReadLine();
                SpawnMember(port++);
            }
        }


        private static Cluster SpawnMember(int port)
        {
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var serialization2 = new Serialization();
            serialization2.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster2 = new Cluster(system2, serialization2);
            var helloProps = Props.FromProducer(() => new HelloActor());
            cluster2.Remote.RegisterKnownKind("hello", helloProps);
            cluster2.StartAsync(new ClusterConfig("mycluster", "127.0.0.1", port, consul2).WithPidCache(false));
            return cluster2;
        }
    }

    public class HelloActor : IActor
    {
        private readonly ILogger _log = Log.CreateLogger<HelloActor>();

        public Task ReceiveAsync(IContext ctx)
        {
            if (ctx.Message is Started)
            {
                _log.LogInformation("I started " + ctx.Self);
            }

            if (ctx.Message is HelloRequest)
            {
                ctx.Respond(new HelloResponse());
            }

            if (ctx.Message is Stopped)
            {
                _log.LogInformation("IM STOPPING!! " + ctx.Self);
            }

            return Actor.Done;
        }
    }
}