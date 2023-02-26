// -----------------------------------------------------------------------
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
    private static readonly int ActorCount = 1;
    private static readonly int MemberCount = 1;
    private static readonly int KillTimeoutSeconds = 30;

    private static int _requestCount;
    private static int _failureCount;
    private static int _successCount;

    private static readonly object Request = new HelloRequest();

    public static async Task Main(string[] args)
    {
        ThreadPool.SetMinThreads(0, 0);

        foreach (var batchSize in new[] {100, 150, 200, 250, 300})
        {
            Configuration.ResetAgent();
            ResetCounters();

            var cluster = await Configuration.SpawnClient();

            var elapsed = await RunWorkers(
                () => new RunMemberInProcGraceful(),
                () => RunBatchClient(batchSize, cluster));
            var tps = _requestCount / elapsed.TotalMilliseconds * 1000;
            Console.WriteLine();
            Console.WriteLine($"Batch Size:\t{batchSize}");
            Console.WriteLine($"Requests:\t{_requestCount:N0}");
            Console.WriteLine($"Successful:\t{_successCount:N0}");
            Console.WriteLine($"Failures:\t{_failureCount:N0}");
            Console.WriteLine($"Throughput:\t{tps:N0} requests/sec -> {(tps * 2):N0} msg/sec");
            await cluster.ShutdownAsync();

            await Task.Delay(5000);
        }
    }

    private static void ResetCounters()
    {
        _requestCount = 0;
        _failureCount = 0;
        _successCount = 0;
    }

    private static async Task SendRequest(Cluster cluster, ClusterIdentity id, CancellationToken cancellationToken, ISenderContext? context = null)
    {
        Interlocked.Increment(ref _requestCount);

        if (context == null)
        {
            context = cluster.System.Root;
        }

        try
        {
            try
            {
                await cluster.RequestAsync<object>(id, Request, context, cancellationToken);

                var res = Interlocked.Increment(ref _successCount);

                if (res % 10000 == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(".");
                    Console.ResetColor();
                }

                return;
            }
            catch (TimeoutException)
            {
                // ignored                
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

            Interlocked.Increment(ref _failureCount);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("X");
            Console.ResetColor();
        }
    }

    private static Task RunBatchClient(int batchSize, Cluster cluster)
    {
        var identities = new ClusterIdentity[ActorCount];

        for (var i = 0; i < ActorCount; i++)
        {
            var id = "myactor" + i;
            identities[i] = ClusterIdentity.Create(id, "hello");
        }

        var logger = Log.CreateLogger(nameof(Program));

        _ = SafeTask.Run(async () => {
                var rnd = new Random();
                var semaphore = new AsyncSemaphore(5);

                while (!cluster.System.Shutdown.IsCancellationRequested)
                {
                    await semaphore.WaitAsync(() => RunBatch(rnd, cluster));
                }
            }
        );

        async Task RunBatch(Random? rnd, Cluster cluster)
        {
            var requests = new List<Task>();

            try
            {
                var ct = CancellationTokens.FromSeconds(20);

                var ctx = cluster.System.Root.CreateBatchContext(batchSize, ct);

                for (var i = 0; i < batchSize; i++)
                {
                    var id = identities[rnd!.Next(0, ActorCount)];
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

        return Task.CompletedTask;
    }

    private static async Task<TimeSpan> RunWorkers(Func<IRunMember> memberFactory, Func<Task> startClient)
    {
        var followers = new List<IRunMember>();

        for (var i = 0; i < MemberCount; i++)
        {
            var p = memberFactory();
            await p.Start();
            await Task.Delay(500);
            Console.WriteLine("Worker started...");
            followers.Add(p);
        }

        await Task.Delay(1000);

        await startClient();
        Console.WriteLine("Client started...");

        var sw = Stopwatch.StartNew();

        await Task.Delay(KillTimeoutSeconds * 1000);
        var first = true;

        foreach (var t in followers)
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