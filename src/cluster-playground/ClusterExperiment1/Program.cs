using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
            //node 1
            var system1 = new ActorSystem();
            var q = new ConcurrentQueue<object>();
            system1.EventStream.Subscribe(e =>
                {
                    q.Enqueue(e);
                    Console.WriteLine(e);
                }
            );

            void Expect<T>(Func<T, bool> pred)
            {
                Console.WriteLine($"Expecting {typeof(T).Name}");
                SpinWait.SpinUntil(() =>
                    {
                        if (q.TryDequeue(out var e) && e is T evt)
                        {
                            if (pred(evt))
                            {
                                return true;
                            }
                        }

                        Thread.Sleep(10);
                        return false;
                    }
                );
                Console.WriteLine($"Success");
            }
            
            
            var consul1 = new ConsulProvider(new ConsulProviderOptions());
            var cluster1 = new Cluster(system1,new Serialization());
            await cluster1.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8090,consul1));
            Expect<MemberJoinedEvent>(e => e.Port == 8090);
            
            //node 2
            var system2 = new ActorSystem();
            var consul2 = new ConsulProvider(new ConsulProviderOptions());
            var cluster2 = new Cluster(system2,new Serialization());
            await cluster2.StartAsync(new ClusterConfig("mycluster","127.0.0.1",8091,consul2));

            Expect<MemberJoinedEvent>(e => e.Port == 8091);

            
                
            
            Console.WriteLine("hello");
            Console.ReadLine();
        }
    }
}