using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;

namespace LocalPingPong
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            const int messageCount = 1000000;
            const int batchSize = 100;
            int[] clientCounts = {8, 16, 32};
            int[] tps = new[] {50, 100, 200, 400, 800};

            foreach (int t in tps)
            {
                ThreadPoolDispatcher d = new ThreadPoolDispatcher {Throughput = t};

                foreach (int clientCount in clientCounts)
                {
                    ActorSystem sys = new ActorSystem();
                    Console.WriteLine("Starting test  " + clientCount);
                    PID[] pingActors = new PID[clientCount];
                    PID[] pongActors = new PID[clientCount];

                    for (int i = 0; i < clientCount; i++)
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

                    Task[] tasks = new Task[clientCount];
                    Stopwatch sw = Stopwatch.StartNew();

                    for (int i = 0; i < clientCount; i++)
                    {
                        PID pingActor = pingActors[i];
                        PID pongActor = pongActors[i];

                        tasks[i] = sys.Root.RequestAsync<bool>(pingActor, new PingActor.Start(pongActor));
                    }

                    Console.WriteLine("Waiting for actors");
                    await Task.WhenAll(tasks);
                    sw.Stop();

                    int totalMessages = messageCount * 2 * clientCount;
                    int x = (int)(totalMessages / (double)sw.ElapsedMilliseconds * 1000.0d);
                    Console.WriteLine();
                    Console.WriteLine($"{clientCount}\t\t{sw.ElapsedMilliseconds}\t\t{x:n0}");
                }
            }
        }
    }
}
