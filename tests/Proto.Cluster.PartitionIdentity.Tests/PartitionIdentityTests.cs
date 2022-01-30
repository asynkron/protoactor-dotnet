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

namespace Proto.Cluster.PartitionIdentity.Tests
{
    public class PartitionIdentityTests
    {
        private readonly ITestOutputHelper _output;

        public PartitionIdentityTests(ITestOutputHelper output) => _output = output;

        private long _requests = 0;

        [Theory]
        [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Pull, PartitionIdentityLookup.Send.Full)]
        [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Pull, PartitionIdentityLookup.Send.Delta)]
        [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Push, PartitionIdentityLookup.Send.Full)]
        [InlineData(100, 5, 12, 20, PartitionIdentityLookup.Mode.Push, PartitionIdentityLookup.Send.Delta)]
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
            await using var fixture = await InitClusterFixture(memberCount, mode, send);

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
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                var now = Interlocked.Read(ref _requests);

                _output.WriteLine($"Consistent responses: {((now - prev) / (double)timer.ElapsedMilliseconds) * 1000d:N0} / s");
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
        ) => _ = Task.Run(async () => {
                var rnd = new Random();
                var identityIndex = rnd.Next(identities.Count);
                var tasks = new List<Task>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; i < batchSize; i++)
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

        private static Cluster RandomMember(IClusterFixture fixture, Random rnd) => RandomMember(fixture.Members, rnd);

        private static Cluster RandomMember(IList<Cluster> members, Random rnd)
            => members[rnd.Next(members.Count)];

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
                    _output.WriteLine($"Inconsistent state {id}/{response.SessionId} {response.Count} instead of {response.ExpectedCount}");
                    // response.Count.Should().Be(response.ExpectedCount, $"Inconsistent state {id}/{response.SessionId} {response.Count} instead of {response.ExpectedCount}")
                }
            }
        }

        private void StartKillingRandomVirtualActors(
            IClusterFixture clusterFixture,
            List<string> identities,
            CancellationToken cancellationToken
        ) => _ = Task.Run(async () => {
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
        ) => _ = Task.Run(async () => {
                const int maxMembers = 10;
                const int minMembers = 2;
                var rnd = new Random();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(rnd.Next(10000), cancellationToken).ConfigureAwait(false);
                        var spawn = rnd.Next() % 2 == 0;

                        if (spawn)
                        {
                            if (clusterFixture.Members.Count < maxMembers)
                            {
                                _output.WriteLine("Spawning member");
                                _ = clusterFixture.SpawnNode();
                            }
                        }
                        else
                        {
                            // var graceful = rnd.Next() % 2 != 0;
                            const bool graceful = true;

                            if (clusterFixture.Members.Count > minMembers)
                            {
                                _output.WriteLine("Stopping member " + (graceful ? "gracefully" : "with wanton disregard"));
                                _ = StopRandomMember(clusterFixture, clusterFixture.Members.Skip(2).ToList(), rnd, graceful);
                            }
                        }

                        if (rnd.Next() % 2 == 0)
                        {
                            await Task.Delay(rnd.Next(100), cancellationToken).ConfigureAwait(false);

                            if (spawn)
                            {
                                if (clusterFixture.Members.Count < maxMembers)
                                {
                                    _output.WriteLine("Spawning another member");

                                    _ = clusterFixture.SpawnNode();
                                }
                            }
                            else
                            {
                                // var graceful = rnd.Next() % 2 != 0;
                                const bool graceful = true;

                                if (clusterFixture.Members.Count > minMembers)
                                {
                                    _output.WriteLine("Stopping another member " + (graceful ? "gracefully" : "badly"));
                                    _ = StopRandomMember(clusterFixture, clusterFixture.Members.Skip(2).ToList(), rnd, graceful);
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

        private static Task StopRandomMember(IClusterFixture fixture, IList<Cluster> candidates, Random rnd, bool graceful)
            => _ = fixture.RemoveNode(RandomMember(candidates, rnd), graceful);

        private static async Task<PartitionIdentityClusterFixture> InitClusterFixture(
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
        private readonly PartitionIdentityLookup.Mode _mode;
        private readonly PartitionIdentityLookup.Send _send;
        public readonly ActorStateRepo Repository = new();
        private readonly int _chunkSize;

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

        protected override IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionIdentityLookup( new PartitionConfig
        {
            GetPidTimeout = TimeSpan.FromSeconds(5),
            HandoverChunkSize = _chunkSize,
            RebalanceRequestTimeout = TimeSpan.FromSeconds(3),
            Mode = _mode,
            Send = _send
        });

        protected override ClusterKind[] ClusterKinds
            => new[] {new ClusterKind(ConcurrencyVerificationActor.Kind, Props.FromProducer(() => new ConcurrencyVerificationActor(Repository)))};
    }
}