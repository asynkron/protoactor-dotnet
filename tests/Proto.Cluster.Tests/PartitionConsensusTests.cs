using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class PartitionConsensusTests
{
    [Fact]
    public async Task PartitionedMember_ResumesGossipAfterDrop()
    {
        var fixture = new PartitionClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        await Task.WhenAll(fixture.Members.Select(m => m.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5))));

        var members = fixture.Members;
        var memberA = members[0];
        var memberB = members[1];
        var memberC = members[2];

        var probeHelper = new GossipProbe(memberA);
        using var stateProbe = await probeHelper.CreateStateProbeAsync();

        await GossipProbe.WaitForMemberStateAsync(memberB, stateProbe.Key, memberA.System.Id,
            s => s == stateProbe.Value, TimeSpan.FromSeconds(10));
        await GossipProbe.WaitForMemberStateAsync(memberC, stateProbe.Key, memberA.System.Id,
            s => s == stateProbe.Value, TimeSpan.FromSeconds(10));

        var oldValue = stateProbe.Value;

        GossipNetworkPartition.Isolate(memberB.System.Address);

        await stateProbe.PublishRandomValueAsync();

        // Ensure the updated state is observed by at least one other member
        await GossipProbe.WaitForMemberStateAsync(memberC, stateProbe.Key, memberA.System.Id,
            s => s == stateProbe.Value, TimeSpan.FromSeconds(5));

        var stateDuringPartition = await GossipProbe.GetStateAsync(memberB, stateProbe.Key);
        stateDuringPartition[memberA.System.Id].Should().Be(oldValue);

        GossipNetworkPartition.Clear();

        // Re-emit the updated state so the previously partitioned member catches up
        await memberA.Gossip.SetStateAsync(stateProbe.Key, GossipProbe.CreateStateMessage(stateProbe.Value));
        await memberC.Gossip.SetStateAsync(stateProbe.Key, GossipProbe.CreateStateMessage(stateProbe.Value));

        await GossipProbe.WaitForMemberStateAsync(memberB, stateProbe.Key, memberA.System.Id,
            s => s == stateProbe.Value, TimeSpan.FromSeconds(10));
        var stateAfterRecovery = await GossipProbe.GetStateAsync(memberB, stateProbe.Key);
        stateAfterRecovery[memberA.System.Id].Should().Be(stateProbe.Value);
    }

    private class PartitionClusterFixture : BaseInMemoryClusterFixture
    {
        public PartitionClusterFixture() : base(3)
        {
        }

        protected override ActorSystemConfig GetActorSystemConfig()
        {
            var baseConfig = base.GetActorSystemConfig();
            return baseConfig
                .WithConfigureProps(p => baseConfig.ConfigureProps(p).WithSenderMiddleware(GossipNetworkPartition.Middleware))
                .WithConfigureSystemProps((name, p) => baseConfig.ConfigureSystemProps(name, p).WithSenderMiddleware(GossipNetworkPartition.Middleware));
        }
    }

    private static class GossipNetworkPartition
    {
        private static readonly HashSet<string> Dropped = new();

        public static void Isolate(string address) => Dropped.Add(address);
        public static void Clear() => Dropped.Clear();

        public static Func<Sender, Sender> Middleware => next => async (ctx, target, envelope) =>
        {
            if (envelope.Message is GossipRequest && target.Id == Gossiper.GossipActorName)
            {
                var from = ctx.System.Address;
                var to = target.Address;

                if (Dropped.Contains(from) || Dropped.Contains(to))
                {
                    return;
                }
            }

            await next(ctx, target, envelope);
        };
    }
}
