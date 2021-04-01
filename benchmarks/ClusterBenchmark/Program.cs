// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Utils;

namespace ClusterExperiment1
{
    public static class Program
    {
        private static TaskCompletionSource<bool> _ts;
        private static int ActorCount;
        private static int MemberCount;
        private static int KillTimeoutSeconds;

        private static int RequestCount;
        private static int FailureCount;
        private static int SuccessCount;

        public static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            //    ThreadPool.SetMinThreads(500, 500);

            if (args.Length > 0)
            {
                // InteractiveOutput = args[0] == "1";

                Cluster worker = await Configuration.SpawnMember();
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => { worker.ShutdownAsync().Wait(); };
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            Configuration.SetupLogger();

            _ts = new TaskCompletionSource<bool>();

            _ = DockerSupport.Run(_ts.Task);

            Console.WriteLine("Proto.Cluster chaos benchmark");
            Console.WriteLine();
            Console.WriteLine("Explanation:");
            Console.WriteLine(". = 10 000 successful requests");
            // Console.WriteLine("# = activation of a virtual actor");
            // Console.WriteLine("+ = (deliberate) deactivation of virtual actor");
            Console.WriteLine("X = NULL response, e.g. requests retried but got no response");
            Console.WriteLine();
            // Console.WriteLine("1) Run with interactive output");
            // Console.WriteLine("2) Run silent");
            //
            // var res0 = Console.ReadLine();
            // InteractiveOutput = res0 == "1";

            Console.WriteLine("1) Run single process - graceful exit");
            Console.WriteLine("2) Run single process");
            Console.WriteLine("3) Run multi process - graceful exit");
            Console.WriteLine("4) Run multi process");

            string? memberRunStrategy = Console.ReadLine();

            Console.WriteLine("1) Run single request client");
            Console.WriteLine("2) Run batch requests client");
            Console.WriteLine("3) Run fire and forget client");

            string? clientStrategy = Console.ReadLine();

            int batchSize = 0;

            if (clientStrategy == "2")
            {
                Console.WriteLine("Batch size? default is 50");

                if (!int.TryParse(Console.ReadLine(), out batchSize))
                {
                    batchSize = 50;
                }

                Console.WriteLine($"Using batch size {batchSize}");
            }

            Console.WriteLine("Number of virtual actors? default 10000");
            if (!int.TryParse(Console.ReadLine(), out ActorCount))
            {
                ActorCount = 10_000;
            }

            Console.WriteLine($"Using {ActorCount} actors");

            Console.WriteLine("Number of cluster members? default is 8");
            if (!int.TryParse(Console.ReadLine(), out MemberCount))
            {
                MemberCount = 8;
            }

            Console.WriteLine($"Using {MemberCount} members");

            Console.WriteLine("Seconds to run before stopping members? default is 30");
            if (!int.TryParse(Console.ReadLine(), out KillTimeoutSeconds))
            {
                KillTimeoutSeconds = 30;
            }

            Console.WriteLine($"Using {KillTimeoutSeconds} seconds");


            Action run = clientStrategy switch
            {
                "1" => () => RunClient(),
                "2" => () => RunBatchClient(batchSize),
                "3" => () => RunFireForgetClient(),
                _   => throw new ArgumentOutOfRangeException()
            };

            TimeSpan elapsed = await (memberRunStrategy switch
            {
                "1" => RunWorkers(() => new RunMemberInProcGraceful(), run),
                "2" => RunWorkers(() => new RunMemberInProc(), run),
                "3" => RunWorkers(() => new RunMemberExternalProcGraceful(), run),
                "4" => RunWorkers(() => new RunMemberExternalProc(), run),
                _   => throw new ArgumentOutOfRangeException()
            });

            double tps = RequestCount / elapsed.TotalMilliseconds * 1000;
            Console.WriteLine();
            Console.WriteLine($"Requests:\t{RequestCount:N0}");
            Console.WriteLine($"Successful:\t{SuccessCount:N0}");
            Console.WriteLine($"Failures:\t{FailureCount:N0}");
            Console.WriteLine($"Throughput:\t{tps:N0} msg/sec");
        }

        private static void RunFireForgetClient()
        {
            ILogger logger = Log.CreateLogger(nameof(Program));

            _ = SafeTask.Run(async () =>
                {
                    AsyncSemaphore semaphore = new(50);
                    Cluster cluster = await Configuration.SpawnClient();
                    Random rnd = new();

                    while (true)
                    {
                        string id = "myactor" + rnd.Next(0, ActorCount);
                        semaphore.Wait(() => SendRequest(cluster, id, logger));
                    }
                }
            );
        }

        private static Task SendRequest(Cluster cluster, string id, ILogger logger)
        {
            Interlocked.Increment(ref RequestCount);

            Task<HelloResponse> t = cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                CancellationTokens.WithTimeout(20000)
            );


            t.ContinueWith(t =>
                {
                    if (t.IsFaulted || t.Result is null)
                    {
                        Interlocked.Increment(ref FailureCount);

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("X");
                        Console.ResetColor();

                        PartitionIdentityLookup il = cluster.Config.IdentityLookup as PartitionIdentityLookup;

                        il?.DumpState(ClusterIdentity.Create(id, "hello"));
                    }
                    else
                    {
                        int res = Interlocked.Increment(ref SuccessCount);

                        if (res % 10000 == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.Write(".");
                            Console.ResetColor();
                        }
                    }
                }
            );

            return t;
        }


        private static void RunBatchClient(int batchSize)
        {
            ILogger logger = Log.CreateLogger(nameof(Program));

            _ = SafeTask.Run(async () =>
                {
                    Cluster cluster = await Configuration.SpawnClient();
                    Random rnd = new();

                    while (true)
                    {
                        List<Task> requests = new();

                        try
                        {
                            for (int i = 0; i < batchSize; i++)
                            {
                                string id = "myactor" + rnd.Next(0, ActorCount);
                                Task request = SendRequest(cluster, id, logger);

                                requests.Add(request);
                            }

                            await Task.WhenAll(requests);
                        }
                        catch (Exception x)
                        {
                            logger.LogError(x, "Error...");
                        }
                    }
                }
            );
        }

        private static void RunClient()
        {
            ILogger logger = Log.CreateLogger(nameof(Program));

            _ = SafeTask.Run(async () =>
                {
                    Cluster cluster = await Configuration.SpawnClient();
                    Random rnd = new();

                    while (true)
                    {
                        string id = "myactor" + rnd.Next(0, ActorCount);
                        await SendRequest(cluster, id, logger);
                    }
                }
            );
        }

        private static async Task<TimeSpan> RunWorkers(Func<IRunMember> memberFactory, Action startClient)
        {
            List<IRunMember> followers = new();

            for (int i = 0; i < MemberCount; i++)
            {
                IRunMember p = memberFactory();
                await p.Start();
                followers.Add(p);
            }

            await Task.Delay(8000);

            startClient();
            Console.WriteLine("Client started...");

            Stopwatch sw = Stopwatch.StartNew();

            await Task.Delay(KillTimeoutSeconds * 1000);
            bool first = true;
            foreach (IRunMember t in followers)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    await Task.Delay(KillTimeoutSeconds * 1000);
                }

                Console.WriteLine("Stopping node...");
                _ = t.Kill();
            }

            sw.Stop();
            return sw.Elapsed;
        }
    }
}
