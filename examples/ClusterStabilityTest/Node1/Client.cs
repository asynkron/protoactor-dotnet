using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Polly;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using Log = Proto.Log;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{
    public static class Client
    {
        public static async Task Start()
        {
            var log = LoggerFactory.Create(x => x.AddSeq().SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(log);

            var logger = log.CreateLogger("Client");

            logger.LogInformation("Test");
            const string clusterName = "test";

            var system = new ActorSystem();
            var serialization = new Serialization();
            var cluster = new Cluster(system, serialization);
            var grains = new Grains(cluster);

            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            await cluster.Start(
                clusterName, "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions { DeregisterCritical = TimeSpan.FromSeconds(2) })
            );

            system.EventStream.Subscribe<ClusterTopologyEvent>(e => logger.LogInformation("Topology changed {@Event}", e));
            system.EventStream.Subscribe<MemberStatusEvent>(e => logger.LogInformation("Member status {@Event}", e));

            var options = new GrainCallOptions
            {
                RetryCount = 10,
                RetryAction = i =>
                {
                    Console.Write("!");
                    return Task.Delay(50);
                }
            };

            Console.WriteLine("Ready to send messages, press Enter");
            Console.ReadLine();

            var policy = Policy.Handle<TaskCanceledException>().RetryForeverAsync();

            for (var i = 0; i < 100000; i++)
            {
                var client = grains.HelloGrain("name" + i % 200);

                await policy.ExecuteAsync(
                    () => client.SayHello(new HelloRequest(), CancellationToken.None, options)
                );
                Console.Write(".");
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
            await cluster.Shutdown();
        }
    }
}