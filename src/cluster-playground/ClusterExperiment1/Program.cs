using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Events;
using Proto.Remote;

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
    //        var probe1 = system1.EventStream.GetProbe();
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var serialization1 = new Serialization();
            serialization1.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster1 = new Cluster(system1,serialization1);

            //act
            await cluster1.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8090,consul1).WithPidCache(false));
   
      //      await probe1.Expect<MemberJoinedEvent>(e => e.Member.Port == 8090);
            //node 2
            var cluster2 = SpawnMember(8091);
      //      await probe1.Expect<MemberJoinedEvent>(e => e.Member.Port == 8091);
            var cluster3 = SpawnMember(8092);
      //      await probe1.Expect<MemberJoinedEvent>(e => e.Member.Port == 8092);
      await Task.Delay(2000);
      
            Task.Run(async () =>
                {
                    for (int i = 0; i < 90; i++)
                    {
                        SpawnMember(8093+i);
     
                        await Task.Delay(3000);
                    }
                }
            );
            //await cluster2.ShutdownAsync(true); //kill this node, can also be non graceful to simulate outage
            var cluster4 = SpawnMember(8093);
            
            
            var (pid,status) = await cluster1.GetAsync("myactor2", "hello");
            if (status != ResponseStatusCode.OK)
            {
                Console.WriteLine("BUG!");
                return;
            }

            Console.WriteLine(pid);

            var response = await system1.Root.RequestAsync<HelloResponse>(pid, new HelloRequest());
            
            Console.WriteLine("Got response!");

       
          //  await probe1.Expect<MemberLeftEvent>(e => e.Member.Port == 8091);
//            await probe1.Expect<EndpointTerminatedEvent>(e => e.Address.EndsWith("8091"));

            var rnd = new Random();
            while (true)
            {
                try
                {
                    var id = "myactor" + rnd.Next(0, 10);
                //    Console.WriteLine($"Sending request {id}");
                    var res = await cluster1.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                        CancellationToken.None
                    );

                    if (res == null)
                    {
                        Console.WriteLine("Got void response");
                    }
                    else
                    {
                  //      Console.WriteLine("Got response");
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine(x);
                }
            }

            Console.ReadLine();
        }

        private static Cluster SpawnMember(int port)
        {
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var serialization2 = new Serialization();
            serialization2.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var cluster2 = new Cluster(system2, serialization2);
            var helloProps = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is Started)
                    {
                        Console.WriteLine("I started " + ctx.Self);
                    }
                    if (ctx.Message is HelloRequest)
                    {
                        ctx.Respond(new HelloResponse());
                    }

                    if (ctx.Message is Stopped)
                    {
                        Console.WriteLine("IM STOPPING!! " + ctx.Self);
                    }

                    return Actor.Done;
                }
            );
            cluster2.Remote.RegisterKnownKind("hello", helloProps);
            cluster2.StartAsync(new ClusterConfig("mycluster","127.0.0.1",port,consul2));
            return cluster2;
        }
    }
}