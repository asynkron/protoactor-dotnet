using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;

namespace LocalPingPong
{
    class Program
    {
        private static async Task Main()
        {
            const int messageCount = 1000000;
            const int batchSize = 100;
            int[] clientCounts = {8, 16, 32};
            var tps = new[] {50, 100, 200, 400, 800};

            foreach (var t in tps)
            {
                var d = new ThreadPoolDispatcher {Throughput = t};

                foreach (var clientCount in clientCounts)
                {
                    var sys = new ActorSystem();
                    Console.WriteLine("Starting test  " + clientCount);
                    var pingActors = new PID[clientCount];
                    var pongActors = new PID[clientCount];

                    for (var i = 0; i < clientCount; i++)
                    {
                        pingActors[i] = sys.Root.Spawn(
                            PingActor
                                .Props(messageCount, batchSize)
                                .WithDispatcher(d)
                        );
                        pongActors[i] = sys.Root.Spawn(
                            PongActor
                                .Props
                                .WithDispatcher(d)
                        );
                    }

                    Console.WriteLine("Actors created");

                    var tasks = new Task[clientCount];
                    var sw = Stopwatch.StartNew();

                    for (var i = 0; i < clientCount; i++)
                    {
                        var pingActor = pingActors[i];
                        var pongActor = pongActors[i];

                        tasks[i] = sys.Root.RequestAsync<bool>(pingActor, new PingActor.Start(pongActor));
                    }

                    Console.WriteLine("Waiting for actors");
                    await Task.WhenAll(tasks);
                    sw.Stop();

                    var totalMessages = messageCount * 2 * clientCount;
                    var x = (int) (totalMessages / (double) sw.ElapsedMilliseconds * 1000.0d);
                    Console.WriteLine();
                    Console.WriteLine($"{clientCount}\t\t{sw.ElapsedMilliseconds}\t\t{x:n0}");
                }
            }
        }
    }
}