using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.IO;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using static Newtonsoft.Json.JsonConvert;

namespace ClusterExperiment1
{
    public static class Program
    {

        public static async Task Main()
        {

            //arrange
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));

            //node 1
            var system1 = new ActorSystem();
            var probe1 = system1.EventStream.GetProbe();
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var cluster1 = new Cluster(system1,new Serialization());
            
            //node 2
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var cluster2 = new Cluster(system2,new Serialization());
            
            
            //act
            await cluster1.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8090,consul1));
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8090);
            await cluster2.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8091,consul2));
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8091);
             cluster2.Shutdown(false);
            await probe1.Expect<MemberLeftEvent>(e => e.Port == 8091);
            await probe1.Expect<EndpointTerminatedEvent>(e => e.Address.EndsWith("8091"));

            Console.ReadLine();
        }
    }
}