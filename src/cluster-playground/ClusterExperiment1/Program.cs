using System;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
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


            var helloProps = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is HelloRequest)
                    {
                        ctx.Respond(new HelloResponse());
                    }

                    return Actor.Done;
                }
            );
            
            //node 1
            var system1 = new ActorSystem();
            var probe1 = system1.EventStream.GetProbe();
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var serialization1 = new Serialization();
            serialization1.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster1 = new Cluster(system1,serialization1);
            
            //node 2
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var serialization2 = new Serialization();
            serialization2.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster2 = new Cluster(system2,serialization2);
            cluster2.Remote.RegisterKnownKind("hello", helloProps);
            
            
            //act
            await cluster1.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8090,consul1));
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8090);
            await cluster2.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8091,consul2));
            await probe1.Expect<MemberJoinedEvent>(e => e.Port == 8091);


            var (pid,status) = await cluster1.GetAsync("myactor", "hello");

            var response = await system1.Root.RequestAsync<HelloResponse>(pid, new HelloRequest());
            
            Console.WriteLine("Got response!");

            cluster2.Shutdown(false); //skip await on purpose, we want to see that expected events are still correct w/o waiting
            await probe1.Expect<MemberLeftEvent>(e => e.Port == 8091);
            await probe1.Expect<EndpointTerminatedEvent>(e => e.Address.EndsWith("8091"));
            
        }
    }
}