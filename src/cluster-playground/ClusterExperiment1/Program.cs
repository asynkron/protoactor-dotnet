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
            Console.WriteLine("Press enter to start");
            Console.WriteLine();
            Console.WriteLine("Red = spawned grains");
            Console.WriteLine("Yellow = cluster topology events");
            Console.WriteLine("Each '.' is a request/response call to one of the grains");
            Console.WriteLine("Enter spawns a new node in the cluster");
            Console.ReadLine();
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));
            var logger = Log.CreateLogger(nameof(Program));

            var system1 = new ActorSystem();
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var serialization1 = new Serialization();
            serialization1.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var c1 = new Cluster(system1, serialization1);
            await c1.StartAsync(new ClusterConfig("mycluster", "127.0.0.1", 8090, consul1).WithPidCache(false));
            var c2= SpawnMember(8091);
            var c3 = SpawnMember(8092);
            var c4 = SpawnMember(8093);

            var c = new[] {c1, c2, c3};


            _ = Task.Run(async () =>
                {
                    await Task.Delay(15000);
                    await c4.ShutdownAsync(false);

                    // await Task.Delay(5000);
                    // c4.ShutdownAsync(true);
                }
            );



            _ = Task.Run(async () =>
                {
                    var rnd = new Random();
                    while (true)
                    {
                        try
                        {
                            var id = "myactor" + rnd.Next(0, 1000);
                            var i = rnd.Next(0, 2);
                            //    Console.WriteLine($"Sending request {id}");
                            var res = await c1.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                            );

                            if (res == null)
                            {
                                logger.LogError("Got void response");
                            }
                            else
                            {
                                Console.Write(".");
                                //      Console.WriteLine("Got response");
                            }
                        }
                        catch (Exception)
                        {
                            logger.LogError("banana");
                        }

                        //await Task.Delay(0);
                    }
                }
            );

            int port = 8094;

            while (true)
            {
                Console.ReadLine();
                Console.WriteLine(
                    "-----------------------------------------------------------------------------------------"
                );
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
     //   private readonly ILogger _log = Log.CreateLogger<HelloActor>();

        public Task ReceiveAsync(IContext ctx)
        {
            if (ctx.Message is Started)
            {
                Console.Write("#");
                //just to highlight when this happens
             //   _log.LogError("I started " + ctx.Self);
            }

            if (ctx.Message is HelloRequest)
            {
                ctx.Respond(new HelloResponse());
            }

            if (ctx.Message is Stopped)
            {
                //just to highlight when this happens
            //    _log.LogError("IM STOPPING!! " + ctx.Self);
            }

            return Actor.Done;
        }
    }
}