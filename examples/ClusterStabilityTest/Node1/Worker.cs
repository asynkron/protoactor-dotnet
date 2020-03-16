using System;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Log = Proto.Log;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request) => Task.FromResult(new HelloResponse { Message = "" });
    }

    public static class Worker
    {
        public static async Task Start(string port, string seqPort)
        {
            const string clusterName = "test";

            var log = LoggerFactory.Create(x => x.AddSeq($"http://localhost:{seqPort}").SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(log);

            Console.WriteLine("Starting worker");

            var system = new ActorSystem();
            var serialization = new Serialization();
            var cluster = new Cluster(system, serialization);
            var grains = new Grains(cluster);

            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            grains.HelloGrainFactory(() => new HelloGrain());

            await cluster.Start(
                clusterName, "127.0.0.1", int.Parse(port),
                new ConsulProvider(new ConsulProviderOptions { DeregisterCritical = TimeSpan.FromSeconds(2) })
            );

            Console.WriteLine("Started worked on " + system.ProcessRegistry.Address);

            Console.ReadLine();

            await cluster.Shutdown();
        }
    }
}