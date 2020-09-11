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
        private static async Task RunFollower()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole(o =>
                        {
                            o.IncludeScopes = false;
                            o.UseUtcTimestamp = false;
                            o.TimestampFormat = "hh:mm:ss:fff - ";
                        }
                    ).SetMinimumLevel(LogLevel.Information)
                )
            );

            var cluster = SpawnMember(0);

            Console.ReadLine();
        }

        private static async Task RunLeader()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole(o =>
                        {
                            o.IncludeScopes = false;
                            o.UseUtcTimestamp = false;
                            o.TimestampFormat = "hh:mm:ss:fff - ";
                        }
                    ).SetMinimumLevel(LogLevel.Information)
                )
            );
            var logger = Log.CreateLogger(nameof(Program));

            var system = new ActorSystem();
            var consul = new ConsulProvider(new ConsulProviderOptions());
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster = new Cluster(system, serialization);
            await cluster.StartClientAsync(new ClusterConfig("mycluster", "127.0.0.1", 8090, consul));

            _ = Task.Run(async () =>
                {
                    var rnd = new Random();
                    while (true)
                    {
                        try
                        {
                            var id = "myactor" + rnd.Next(0, 1000);
                            var res = await cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                            );

                            if (res == null)
                            {
                                logger.LogError("Null response");
                            }
                            else
                            {
                                Console.Write(".");
                            }
                        }
                        catch (Exception)
                        {
                            logger.LogError("Request timeout");
                        }
                    }
                }
            );


            Console.ReadLine();
        }

        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                await RunLeader();
            }
            else
            {
                await RunFollower();
            }
        }


        private static Cluster SpawnMember(int port)
        {
            var system = new ActorSystem();
            var consul = new ConsulProvider(new ConsulProviderOptions());
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster = new Cluster(system, serialization);
            var helloProps = Props.FromProducer(() => new HelloActor());
            cluster.Remote.RegisterKnownKind("hello", helloProps);
            cluster.StartMemberAsync(new ClusterConfig("mycluster", "127.0.0.1", port, consul));
            return cluster;
        }
    }

    public class HelloActor : IActor
    {
        //   private readonly ILogger _log = Log.CreateLogger<HelloActor>();

        public Task ReceiveAsync(IContext ctx)
        {
            if (ctx.Message is Started)
            {
                //just to highlight when this happens
                Console.Write("#");
                //_log.LogInformation("I started " + ctx.Self);
            }

            if (ctx.Message is HelloRequest)
            {
                ctx.Respond(new HelloResponse());
            }

            if (ctx.Message is Stopped)
            {
                //just to highlight when this happens
                //    _log.LogWarning("I stopped" + ctx.Self);
            }

            return Actor.Done;
        }
    }
}