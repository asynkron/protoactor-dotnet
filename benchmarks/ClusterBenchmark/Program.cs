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
using Proto.Cluster;
using Serilog;
using Serilog.Events;
using Log = Proto.Log;

namespace ClusterExperiment1
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Configuration.SetupLogger();

           if (args.Length > 0)
           {
               var worker = await Configuration.SpawnMember();
               Thread.Sleep(Timeout.Infinite);
               return;
           }
           
           
           Console.WriteLine("1) Run single process");
           Console.WriteLine("2) Run multi process");

            var res = Console.ReadLine();

            switch (res)
            {
                case "1": await RunSingleProcess();
                    break;
                case "2": await RunMultiProcess();
                    break;
            }
        }

        private static async Task RunMultiProcess()
        {
            var ts = new TaskCompletionSource<bool>();
            RunWorkers(ts, () => new RunMemberExternalProc());
            RunClient();
            
            await ts.Task;
        }

        private static async Task RunSingleProcess()
        {
            
            var ts = new TaskCompletionSource<bool>();
            RunWorkers(ts, () => new RunMemberInProc());
            RunClient();
            
            await ts.Task;
        }

        private static void RunClient()
        {
            var logger = Log.CreateLogger(nameof(Program));
            
            _ = Task.Run(async () => {
                    await Task.Delay(5000);

                    var cluster = await Configuration.SpawnClient();
                    var rnd = new Random();

                    while (true)
                    {
                        var id = "myactor" + rnd.Next(0, 1000);

                        try
                        {
                            var res = await cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token
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

        private static void RunWorkers(TaskCompletionSource<bool> ts, Func<IRunMember> memberFactory)
        {
            var followers = new List<IRunMember>();

            for (var i = 0; i < 4; i++)
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
                        t.Kill();
                    }

                    ts.SetResult(true);
                }
            );
        }
    }
}