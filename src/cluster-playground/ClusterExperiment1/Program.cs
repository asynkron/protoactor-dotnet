using System;
using System.Threading.Tasks;
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

            //arrange
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            
            //node 1
            var system1 = new ActorSystem();
            var probe1 = new EventProbe(system1.EventStream);
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var cluster1 = new Cluster(system1,new Serialization());
            
            //node 2
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var cluster2 = new Cluster(system2,new Serialization());
            
            
            //act
            await cluster1.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8090,consul1));
            
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8090);
            Console.WriteLine("cluster1, Found cluster1 member join event");
            
            await cluster2.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8091,consul2));
            
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8091);
            Console.WriteLine("cluster1, Found cluster2 member join event");

            cluster2.Shutdown(false);
            
            await probe1.Expect<MemberLeftEvent>(e => e.Port == 8091);
            Console.WriteLine("cluster1, Found cluster2 member left event");

            Console.ReadLine();
        }
    }
}