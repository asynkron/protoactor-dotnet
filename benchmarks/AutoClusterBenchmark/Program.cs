﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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
using Proto.Utils;

namespace ClusterExperiment1;

public static class Program
{
    private static int actorCount = 1;
    private static int memberCount = 1;
    private static int killTimeoutSeconds = 30;

    private static int requestCount;
    private static int failureCount;
    private static int successCount;

    private static object Request = new HelloRequest();

    public static async Task Main(string[] args)
    { 
        ThreadPool.SetMinThreads(0, 0);
        foreach (var batchSize in new[] { 100, 150, 200, 250, 300 })
        {
            Configuration.ResetAgent();
            ResetCounters();
                
            var cluster = await Configuration.SpawnClient();
                
            var elapsed = await RunWorkers(() => new RunMemberInProcGraceful(), () => RunBatchClient(batchSize, cluster));
            var tps = requestCount / elapsed.TotalMilliseconds * 1000;
            Console.WriteLine();
            Console.WriteLine($"Batch Size:\t{batchSize}");
            Console.WriteLine($"Requests:\t{requestCount:N0}");
            Console.WriteLine($"Successful:\t{successCount:N0}");
            Console.WriteLine($"Failures:\t{failureCount:N0}");
            Console.WriteLine($"Throughput:\t{tps:N0} requests/sec -> {(tps * 2):N0} msg/sec");
            await cluster.ShutdownAsync();
                
            await Task.Delay(5000);
        }
    }

    private static void ResetCounters()
    {
        requestCount = 0;
        failureCount = 0;
        successCount = 0;
    }

    private static async Task SendRequest(Cluster cluster, ClusterIdentity id, CancellationToken cancellationToken, ISenderContext? context = null)
    {
        Interlocked.Increment(ref requestCount);

        if (context == null)
        {
            context = cluster.System.Root;
        }

        try
        {
                
            var x = await cluster.RequestAsync<object>(id, Request, context, cancellationToken);

            if (x != null)
            {
                var res = Interlocked.Increment(ref successCount);

                if (res % 10000 == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(".");
                    Console.ResetColor();
                }

                return;
            }

            OnError();
        }
        catch
        {
            OnError();
        }

        void OnError()
        {
            if (cluster.System.Shutdown.IsCancellationRequested) return;

            Interlocked.Increment(ref failureCount);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("X");
            Console.ResetColor();
        }
    }

    private static void RunBatchClient(int batchSize, Cluster cluster)
    {
        var identities = new ClusterIdentity[actorCount];
        for (var i = 0; i < actorCount; i++)
        {
            var id = "myactor" + i;
            identities[i] = ClusterIdentity.Create(id,"hello");
        }
            
        var logger = Log.CreateLogger(nameof(Program));

        _ = SafeTask.Run(async () => {
                   
                var rnd = new Random();
                var semaphore = new AsyncSemaphore(5);

                while (!cluster.System.Shutdown.IsCancellationRequested)
                {
                    semaphore.Wait(() => RunBatch(rnd, cluster));
                }
            }
        );

        async Task RunBatch(Random? rnd, Cluster cluster)
        {
            var requests = new List<Task>();

            try
            {
                var ct = CancellationTokens.FromSeconds(20);

                var ctx = cluster.System.Root.CreateBatchContext(batchSize,ct);
                for (var i = 0; i < batchSize; i++)
                {
                    var id = identities[rnd!.Next(0, actorCount)];
                    var request = SendRequest(cluster, id, ct, ctx);

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

    private static async Task<TimeSpan> RunWorkers(Func<IRunMember> memberFactory, Action startClient)
    {
        var followers = new List<IRunMember>();

        for (var i = 0; i < memberCount; i++)
        {
            var p = memberFactory();
            await p.Start();
            await Task.Delay(500);
            Console.WriteLine("Worker started...");
            followers.Add(p);
        }

        await Task.Delay(1000);

        startClient();
        Console.WriteLine("Client started...");

        var sw = Stopwatch.StartNew();

        await Task.Delay(killTimeoutSeconds * 1000);
        var first = true;

        foreach (var t in followers)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                await Task.Delay(killTimeoutSeconds * 1000);
            }

            Console.WriteLine("Stopping node...");
            _ = t.Kill();
        }

        sw.Stop();
        return sw.Elapsed;
    }
}