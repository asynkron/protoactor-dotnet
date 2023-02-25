﻿// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using ClusterPubSubBatchingProducer;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using ProtosReflection = ClusterPubSubBatchingProducer.ProtosReflection;

Log.SetLoggerFactory(LoggerFactory.Create(l =>
        l.AddConsole().SetMinimumLevel(LogLevel.Information)
    )
);

var system = GetSystem();
var cluster = system.Cluster();

await cluster.StartMemberAsync().ConfigureAwait(false);

long deliveredCount = 0;

Console.WriteLine("Subscribing...");

// subscribe 3 times
for (var i = 0; i < 3; i++)
{
    await cluster.Subscribe("my-topic", context =>
        {
            if (context.Message is PingMessage)
            {
                Interlocked.Increment(ref deliveredCount);
            }

            return Task.CompletedTask;
        }
    ).ConfigureAwait(false);
}

// produce
const int count = 1_000_000;
Console.WriteLine("Starting producer...");

var stopwatch = new Stopwatch();
stopwatch.Start();

var producer = cluster.BatchingProducer("my-topic");

var produceTasks =
    Enumerable.Range(1, count).Select(i => producer.ProduceAsync(new PingMessage { Data = i }));

await Task.WhenAll(produceTasks).ConfigureAwait(false);
stopwatch.Stop();

Console.WriteLine(
    $"Sent: {count}, delivered: {deliveredCount}, msg/s: {deliveredCount / (stopwatch.ElapsedTicks / (double)Stopwatch.Frequency):F1}");

Console.WriteLine("Press any key to shut down...");
Console.Read();

await producer.DisposeAsync().ConfigureAwait(false);
await cluster.ShutdownAsync().ConfigureAwait(false);

static ActorSystem GetSystem() =>
    new ActorSystem()
        .WithRemote(GetRemoteConfig())
        .WithCluster(GetClusterConfig());

static GrpcNetRemoteConfig GetRemoteConfig() =>
    GrpcNetRemoteConfig
        .BindToLocalhost()
        .WithProtoMessages(ProtosReflection.Descriptor);

static ClusterConfig GetClusterConfig()
{
    var clusterConfig =
        ClusterConfig
            .Setup("MyCluster", new TestProvider(new TestProviderOptions(), new InMemAgent()),
                new PartitionIdentityLookup());

    return clusterConfig;
}