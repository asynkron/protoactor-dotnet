using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Proto;

namespace LocalPingPong
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");

            const int messageCount = 1000000;
            const int batchSize = 5000;
            int[] clientCounts = {8, 16 };

            Console.WriteLine("Clients\t\tElapsed\t\tMsg/sec");

            var sys = new ActorSystem();
            foreach (var clientCount in clientCounts)
            {
                Console.WriteLine("Starting test  " + clientCount);
                var pingActors = new PID[clientCount];
                var pongActors = new PID[clientCount];

                for (var i = 0; i < clientCount; i++)
                {
                    pingActors[i] = sys.Root.Spawn(PingActor.Props(messageCount, batchSize));
                    pongActors[i] = sys.Root.Spawn(PongActor.Props);
                }
                
                Console.WriteLine("Actors created");

                var tasks = new Task[clientCount];
                var sw = Stopwatch.StartNew();
                for (var i = 0; i < clientCount; i++)
                {
                    var pingActor = pingActors[i];
                    var pongActor = pongActors[i];

                    tasks[i] = sys.Root.RequestAsync<bool>(pingActor,new PingActor.Start(pongActor));
                }
                Console.WriteLine("Waiting for actors");
                await Task.WhenAll(tasks);
                sw.Stop();

                var totalMessages = messageCount * 2 * clientCount;
                var x = (int)(totalMessages / (double)sw.ElapsedMilliseconds * 1000.0d);
                Console.WriteLine($"{clientCount}\t\t{sw.ElapsedMilliseconds}\t\t{x:n0}");

                Console.Write("Waiting 2 sec");
                await Task.Delay(2000);
                Console.WriteLine(" - Done");
            }

            Console.ReadLine();
        }
    }
}
