using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperimentInMemory.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Testing;
using Proto.Remote;
using System.Collections.Concurrent;

namespace ClusterExperimentInMemory
{
    public class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("Press enter to start");
            Console.WriteLine();
            Console.WriteLine("   '#' = spawned grains");
            Console.WriteLine("   'T' = grain stopped");
            Console.WriteLine("   '.' = a request/response call to one of the grains");
            Console.WriteLine();
            Console.WriteLine("Press '+' to spawn a new node in the cluster");
            Console.WriteLine("Press '-' to remove gracefully a node in the cluster");
            Console.WriteLine("Press '*' to remove a node in the cluster");
            Console.WriteLine("Press '/' to remove the client node and replace it with a new client node");
            Console.WriteLine("Press 'e' to end the program gracefully");
            Console.ReadLine();

            Log.SetLoggerFactory(LoggerFactory.Create(l => l
                        .AddConsole(o =>
                        {
                            o.IncludeScopes = false;
                        })
                        .SetMinimumLevel(LogLevel.Error)
                        ));
            var logger = Log.CreateLogger(nameof(Program));

            var workers = new ConcurrentQueue<Cluster>();
            var requesters = new ConcurrentQueue<Cluster>();
            var port = 8090;
            requesters.Enqueue(SpawnMember(port++, false));
            for (int i = 0; i < 1; i++)
            {
                workers.Enqueue(SpawnMember(port++));
            }

            var run = true;
            _ = Task.Run(async () =>
            {
                var rnd = new Random();
                int i = 1;
                while (run)
                {
                    try
                    {
                        var id = "myactor" + rnd.Next(0, 10000);
                        if (requesters.TryPeek(out var node))
                        {
                            var res = await node.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(), new CancellationTokenSource(2000).Token);
                            if (res == null)
                            {
                                Console.Write("_");
                            }
                            else
                            {
                                Console.Write(".");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "");
                    }
                    finally
                    {
                        i++;
                    }
                }
            }
            );



            while (run)
            {
                switch (Console.ReadKey().KeyChar)
                {
                    case '-':
                        {
                            if (workers.TryDequeue(out Cluster removedNode))
                            {
                                await removedNode.ShutdownAsync();
                            }
                        }
                        break;
                    case '*':
                        {
                            if (workers.TryDequeue(out Cluster removedNode))
                            {
                                _ = removedNode.ShutdownAsync(false);
                            }
                        }
                        break;
                    case '+':
                        workers.Enqueue(SpawnMember(port++));
                        break;
                    case '/':
                        {
                            if (requesters.TryDequeue(out var node))
                            {
                                await node.ShutdownAsync();
                            }
                            requesters.Enqueue(SpawnMember(port++, false));
                        }
                        break;
                    case 'e':
                        run = false;
                        break;
                    default:
                        break;
                }
            }
            while (workers.TryDequeue(out var node))
                await node.ShutdownAsync();
            while (requesters.TryDequeue(out var node))
                await node.ShutdownAsync();
        }
        private static InMemAgent agent = new InMemAgent();
        private static Cluster SpawnMember(int port, bool isWorker = true)
        {
            var system = new ActorSystem();
            var clusterProvider = new TestProvider(new TestProviderOptions(), agent);
            var remote = system.AddRemote("127.0.0.1", port, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
                if (isWorker)
                    remote.RemoteKindRegistry.RegisterKnownKind("hello", Props.FromProducer(() => new HelloActor()));
            });
            var clusterNode = system.AddClustering(new ClusterConfig("mycluster", clusterProvider).WithPidCache(false));
            _ = clusterNode.StartAsync().ConfigureAwait(false);
            return clusterNode;
        }
    }
}