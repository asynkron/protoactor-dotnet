// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Interactive;

namespace ClusterExperiment1
{
    public static class Program
    {
        private static TaskCompletionSource<bool> _ts;
        private static int ActorCount;

        public static async Task Main(string[] args)
        {
            Configuration.SetupLogger();

            if (args.Length > 0)
            {
                
                var worker = await Configuration.SpawnMember();
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                    worker.ShutdownAsync(true).Wait();
                };
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            Console.WriteLine("1) Run single process - graceful exit");
            Console.WriteLine("2) Run single process");
            Console.WriteLine("3) Run multi process - graceful exit");
            Console.WriteLine("4) Run multi process");

            var res1 = Console.ReadLine();

            Console.WriteLine("1) Run single request client");
            Console.WriteLine("2) Run batch requests client");
            Console.WriteLine("3) Run fire and forget client");

            var res2 = Console.ReadLine();

            int batchSize = 0;
            if (res2 == "2")
            {
                Console.WriteLine("Batch size? default is 50");
                var res = Console.ReadLine();

                if (!int.TryParse(res, out batchSize))
                {
                    batchSize = 50;
                }
            
                Console.WriteLine($"Using batch size {batchSize}");
            }
            
            Console.WriteLine("Number of virtual actors? default 10000");

            var res3 = Console.ReadLine();

            if (!int.TryParse(res3, out var actorCount))
            {
                actorCount = 10_000;
            }
            
            Console.WriteLine($"Using {actorCount} actors");
            ActorCount = actorCount;
            

            _ts = new TaskCompletionSource<bool>();

            switch (res1)
            {
                case "1":
                    RunWorkers(() => new RunMemberInProcGraceful());
                    break;
                case "2":
                    RunWorkers(() => new RunMemberInProc());
                    break;
                case "3":
                    RunWorkers(() => new RunMemberExternalProcGraceful());
                    break;
                case "4":
                    RunWorkers(() => new RunMemberExternalProc());
                    break;
            }

            switch (res2)
            {
                case "1":
                    RunClient();
                    break;
                case "2":
                    RunBatchClient(batchSize);
                    break;
                case "3":
                    RunFireForgetClient();
                    break;
            }

            await _ts.Task;
        }
        
        private static void RunFireForgetClient()
        {
            var logger = Log.CreateLogger(nameof(Program));
            ThreadPoolStats.Run(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200), t => {
                    logger.LogCritical("Threadpool is flooded");
                }
            );

            _ = Task.Run(async () => {
                    await Task.Delay(5000);

                    var semaphore = new AsyncSemaphore(50);
                    var cluster = await Configuration.SpawnClient();
                    var rnd = new Random();

                    while (true)
                    {
                        var id = "myactor" + rnd.Next(0, ActorCount);
                        semaphore.Wait(() => 
                            {
                                return cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                    new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                                ).ContinueWith(_ => Console.Write("."));
                            }
                        );
                    }
                }
            );
        }

        private static void RunBatchClient(int batchSize)
        {
            var logger = Log.CreateLogger(nameof(Program));
            ThreadPoolStats.Run(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500), t => {
                    logger.LogCritical("Threadpool is flooded");
                }
            );

            _ = Task.Run(async () => {
                    await Task.Delay(5000);

                    var cluster = await Configuration.SpawnClient();
                    var rnd = new Random();

                    while (true)
                    {
                        var requests = new List<Task>();

                        try
                        {
                            for (var i = 0; i < batchSize; i++)
                            {
                                var id = "myactor" + rnd.Next(0, ActorCount);
                                var request = cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                    new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                                );

                                requests.Add(request);
                            }

                            await Task.WhenAll(requests);
                            Console.Write(".");
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
            var logger = Log.CreateLogger(nameof(Program));
            ThreadPoolStats.Run(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500), t => {
                    logger.LogCritical("Threadpool is flooded");
                }
            );

            _ = Task.Run(async () => {
                    await Task.Delay(5000);

                    var cluster = await Configuration.SpawnClient();
                    var rnd = new Random();

                    while (true)
                    {
                        var id = "myactor" + rnd.Next(0, ActorCount);

                        try
                        {
                            var res = await cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                            );

                            if (res is null)
                                logger.LogError("Null response");
                            else
                                Console.Write(".");
                        }
                        catch (Exception x)
                        {
                            logger.LogError(x, "Request timeout for {Id}", id);
                        }
                    }
                }
            );
        }

        private static void RunWorkers(Func<IRunMember> memberFactory)
        {
            var followers = new List<IRunMember>();

            for (var i = 0; i < 8; i++)
            {
                var p = memberFactory();
                p.Start();
                followers.Add(p);
            }

            _ = Task.Run(async () => {
                    foreach (var t in followers)
                    {
                        await Task.Delay(20000);
                        Console.WriteLine("Stopping node...");
                        _ = t.Kill();
                    }

                    _ts.SetResult(true);
                }
            );
        }
    }
}