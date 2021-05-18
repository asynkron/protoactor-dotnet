// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Future;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class InProcessRequestAsyncBenchmark
    {
        [Params(1000)]
        public int BatchSize { get; set; }

        [Params(true, false)]

        public bool UseSharedFutures { get; set; }

        private ActorSystem System;

        private PID pid;

        [GlobalSetup]
        public void Setup()
        {
            var echoProps = Props.FromFunc(ctx => {
                    if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                    return Task.CompletedTask;
                }
            );

            System = new ActorSystem(new ActorSystemConfig
            {
                SharedFutures = UseSharedFutures
            });
            pid = System.Root.SpawnNamed(echoProps, "thing");
        }

        [GlobalCleanup]
        public Task Cleanup() => System.ShutdownAsync();

        // [Benchmark]
        // public Task ClusterRequestAsync() => _cluster.RequestAsync<int>(_id.Identity, _id.Kind, 1, CancellationToken.None);

        [Benchmark]
        public async Task RequestAsync()
        {
            var tasks = new Task<object>[BatchSize];
            var cancellationToken = CancellationTokens.WithTimeout(TimeSpan.FromSeconds(2));

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = System.Root.RequestAsync<object>(pid, 1, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task FutureBatchRequest()
        {
            var cancellationToken = CancellationTokens.WithTimeout(TimeSpan.FromSeconds(2));
            using var batch = new FutureBatchProcess(System, BatchSize, cancellationToken);

            var futures = new IFuture[BatchSize];

            for (var i = 0; i < BatchSize; i++)
            {
                var future = batch.TryGetFuture() ?? throw new Exception("Unable to get future");

                futures[i] = future;
                System.Root.Request(pid, 1, future.Pid);
            }

            try
            {
                await Task.WhenAll(futures.Select(f => f.Task));
            }
            finally
            {
                foreach (var future in futures)
                {
                    future.Dispose();
                }
            }
        }

        [Benchmark]
        public async Task BatchContextRequestAsync()
        {
            var cancellationToken = CancellationTokens.WithTimeout(TimeSpan.FromSeconds(2));
            using var batch = System.Root.CreateBatchContext(BatchSize, cancellationToken);
            var tasks = new Task<object>[BatchSize];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = batch.RequestAsync<object>(pid, 1, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }
    }
}