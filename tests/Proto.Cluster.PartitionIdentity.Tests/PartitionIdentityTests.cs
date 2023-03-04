// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.PartitionIdentity.Tests;

public class PartitionIdentityTests
{
    private readonly ITestOutputHelper _output;

    private long _requests;

    public PartitionIdentityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Pull, PartitionIdentityLookup.Send.Full)]
    // [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Pull, PartitionIdentityLookup.Send.Delta)]
    //[InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Push, PartitionIdentityLookup.Send.Full)]
    //[InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Push, PartitionIdentityLookup.Send.Delta)]
    public async Task ClusterMaintainsSingleConcurrentVirtualActorPerIdentity(
        int identityCount,
        int batchSize,
        int threads,
        int runtimeSeconds,
        PartitionIdentityLookup.Mode mode,
        PartitionIdentityLookup.Send send
    )
    {
        const int memberCount = 3;

        Interlocked.Exchange(ref _requests, 0);
        var fixture = await InitClusterFixture(memberCount, mode, send);
        await using var __ = fixture;

        var identities = Enumerable.Range(0, identityCount).Select(_ => Guid.NewGuid().ToString("N")).ToList();

        var stop = new CancellationTokenSource(runtimeSeconds * 1000);
        // ReSharper disable once AccessToDisposedClosure

        foreach (var _ in Enumerable.Range(0, threads))
        {
            StartBackgroundRequests(fixture, identities, batchSize, stop.Token);
        }

        StartKillingRandomVirtualActors(fixture, identities, stop.Token);
        StartSpawningAndStoppingMembers(fixture, stop.Token);

        var timer = Stopwatch.StartNew();
        var prev = Interlocked.Read(ref _requests);

        while (!stop.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var now = Interlocked.Read(ref _requests);

            _output.WriteLine(
                $"Consistent responses: {(now - prev) / (double)timer.ElapsedMilliseconds * 1000d:N0} / s");

            timer.Restart();
            prev = now;
        }

        _output.WriteLine($"Stopping cluster of {fixture.Members.Count} members");
        timer.Restart();
        await fixture.DisposeAsync();
        _output.WriteLine($"Stopped cluster in {timer.Elapsed}");

        var actorStates = fixture.Repository.Contents.ToList();

        var totalCalls = actorStates.Select(it => it.TotalCount).Sum();
        var restarts = actorStates.Select(it => it.Events.Count(it => it is ActorStopped) - 1).Sum();

        _output.WriteLine($"{totalCalls} requests, {restarts} restarts against " + actorStates.Count + " identities");

        foreach (var actorState in actorStates)
        {
            if (actorState.Inconsistent)
            {
                Assert.False(actorState.Inconsistent, actorState.ToString());
            }
        }
    }

    private void StartBackgroundRequests(
        IClusterFixture clusterFixture,
        List<string> identities,
        int batchSize,
        CancellationToken cancellationToken
    ) =>
        _ = Task.Run(async () =>
            {
                var rnd = new Random();
                var identityIndex = rnd.Next(identities.Count);
                var tasks = new List<Task>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    for (var i = 0; i < batchSize; i++)
                    {
                        var id = identities[identityIndex++ % identities.Count];

                        tasks.Add(Inc(clusterFixture.Members[0], id, cancellationToken));
                        tasks.Add(Inc(clusterFixture.Members[1], id, cancellationToken));
                    }

                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
        );

    private static Cluster RandomMember(IList<Cluster> members, Random rnd) => members[rnd.Next(members.Count)];

    private async Task Inc(Cluster member, string id, CancellationToken cancellationToken)
    {
        var response = await member.RequestAsync<IncResponse>(new ClusterIdentity
            {
                Identity = id,
                Kind = ConcurrencyVerificationActor.Kind
            }, new IncCount(), cancellationToken
        );

        if (!cancellationToken.IsCancellationRequested)
        {
            response.Should().NotBeNull();
            Interlocked.Increment(ref _requests);

            if (response.Count != response.ExpectedCount)
            {
                _output.WriteLine(
                    $"Inconsistent state {id}/{response.SessionId} {response.Count} instead of {response.ExpectedCount}");
                // response.Count.Should().Be(response.ExpectedCount, $"Inconsistent state {id}/{response.SessionId} {response.Count} instead of {response.ExpectedCount}")
            }
        }
    }

    private void StartKillingRandomVirtualActors(
        IClusterFixture clusterFixture,
        List<string> identities,
        CancellationToken cancellationToken
    ) =>
        _ = Task.Run(async () =>
            {
                var rnd = new Random();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(rnd.Next(50), cancellationToken);
                    var id = RandomIdentity(identities, rnd);
                    var member = clusterFixture.Members[rnd.Next(clusterFixture.Members.Count)];

                    var clusterIdentity = new ClusterIdentity
                    {
                        Identity = id,
                        Kind = ConcurrencyVerificationActor.Kind
                    };

                    await member.RequestAsync<Ack>(clusterIdentity, new Die(), cancellationToken
                    );

                    Interlocked.Increment(ref _requests);
                }
            }
        );

    private static string RandomIdentity(List<string> identities, Random rnd) => identities[rnd.Next(identities.Count)];

    private void StartSpawningAndStoppingMembers(
        IClusterFixture clusterFixture,
        CancellationToken cancellationToken
    ) =>
        _ = Task.Run(async () =>
            {
                const int maxMembers = 10;
                const int minMembers = 2;
                var rnd = new Random();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(rnd.Next(10000), cancellationToken);
                        var spawn = rnd.Next() % 2 == 0;

                        for (var i = 0; i <= rnd.Next() % 2; i++)
                        {
                            if (spawn)
                            {
                                if (clusterFixture.Members.Count < maxMembers)
                                {
                                    _output.WriteLine($"[{DateTimeOffset.Now:O}] Starting cluster member");

                                    _ = clusterFixture.SpawnNode()
                                        .ContinueWith(
                                            t =>
                                            {
                                                _output.WriteLine(
                                                    $"[{DateTimeOffset.Now:O}] Spawned cluster member {t.Result.System.Id}");
                                            },
                                            TaskContinuationOptions.NotOnFaulted
                                        );
                                }
                            }
                            else
                            {
                                // var graceful = rnd.Next() % 2 != 0;
                                const bool graceful = true;

                                if (clusterFixture.Members.Count > minMembers)
                                {
                                    _ = StopRandomMember(clusterFixture, clusterFixture.Members.Skip(2).ToList(), rnd,
                                        graceful);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _output.WriteLine(e.ToString());
                }
            }, cancellationToken
        );

    private async Task StopRandomMember(IClusterFixture fixture, IList<Cluster> candidates, Random rnd, bool graceful)
    {
        var member = RandomMember(candidates, rnd);

        _output.WriteLine($"[{DateTimeOffset.Now:O}] Stopping cluster member {member.System.Id} " +
                          (graceful ? "gracefully" : "with wanton disregard"));

        await fixture.RemoveNode(member, graceful);
        _output.WriteLine($"[{DateTimeOffset.Now:O}] Stopped cluster member {member.System.Id}");
    }

    private async Task<PartitionIdentityClusterFixture> InitClusterFixture(
        int memberCount,
        PartitionIdentityLookup.Mode mode,
        PartitionIdentityLookup.Send send
    )
    {
        var fixture = new PartitionIdentityClusterFixture(memberCount, mode, send);
        await fixture.InitializeAsync();

        return fixture;
    }
}

public class PartitionIdentityClusterFixture : BaseInMemoryClusterFixture
{
    private readonly int _chunkSize;
    private readonly PartitionIdentityLookup.Mode _mode;
    private readonly PartitionIdentityLookup.Send _send;
    public readonly ActorStateRepo Repository = new();

    public PartitionIdentityClusterFixture(
        int memberCount,
        PartitionIdentityLookup.Mode mode,
        PartitionIdentityLookup.Send send,
        int chunkSize = 1000
    ) : base(memberCount)
    {
        _mode = mode;
        _send = send;
        _chunkSize = chunkSize;
    }

    protected override ClusterKind[] ClusterKinds
        => new[]
        {
            new ClusterKind(ConcurrencyVerificationActor.Kind,
                Props.FromProducer(() => new ConcurrencyVerificationActor(Repository, this)))
        };

    protected override IIdentityLookup GetIdentityLookup(string clusterName) =>
        new PartitionIdentityLookup(new PartitionConfig
        {
            GetPidTimeout = TimeSpan.FromSeconds(5),
            HandoverChunkSize = _chunkSize,
            RebalanceRequestTimeout = TimeSpan.FromSeconds(3),
            Mode = _mode,
            Send = _send
        });
}