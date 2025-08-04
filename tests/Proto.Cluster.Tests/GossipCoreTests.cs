// -----------------------------------------------------------------------
// <copyright file="GossipCoreTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ClusterTest.Messages;
using Proto.Cluster.Gossip;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossipCoreTests
{
    private readonly ITestOutputHelper _output;

    public GossipCoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Large_cluster_should_get_topology_consensus()
    {
        const int memberCount = 300;
        const int fanout = 3;

        var members =
            Enumerable
                .Range(0, memberCount)
                .Select(_ => new Member { Id = Guid.NewGuid().ToString("N") })
                .ToList();

        var environment =
            members
                .ToDictionary(
                    m => m.Id,
                    m => (
                        Gossip: new Gossip.Gossip(m.Id, fanout, memberCount, null,
                            () => members.Select(m => m.Id).ToImmutableHashSet(), false),
                        Member: m));

        var sends = 0L;

        var topology = new ClusterTopology
        {
            TopologyHash = Member.TopologyHash(members),
            Members = { members }
        };

        foreach (var m in environment.Values)
        {
            await m.Gossip.UpdateClusterTopology(topology.Clone());
        }

        var first = environment.Values.First().Gossip;

        var checkDefinition =
            Gossiper.ConsensusCheckBuilder<ulong>.Create(GossipKeys.Topology,
                (ClusterTopology tp) => tp.TopologyHash);

        var id = Guid.NewGuid().ToString();
        var (handle, check) = checkDefinition.Build(() => first.RemoveConsensusCheck(id));
        first.AddConsensusCheck(id, check);

        var gossipGenerations = 0L;
        var ct = CancellationTokens.FromSeconds(10);

        _ = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    // ReSharper disable once AccessToModifiedClosure
                    Interlocked.Increment(ref gossipGenerations);

                    foreach (var m in environment.Values)
                    {
                        foreach (var (member, delta) in m.Gossip.SendState())
                        {
                            Interlocked.Increment(ref sends);
                            var target = environment[member.Id];
                            target.Gossip.ReceiveState(delta.State);
                            delta.CommitOffsets();
                        }
                    }
                }
            }
            , ct);

        var x = await handle.TryGetConsensus(ct);

        _output.WriteLine("Consensus topology hash " + x.value);
        _output.WriteLine("Gossip generations " + Interlocked.Read(ref gossipGenerations));
        _output.WriteLine("Send count " + Interlocked.Read(ref sends));
        x.consensus.Should().BeTrue();

    }

    [Fact]
    public async Task Small_cluster_should_get_data_consensus()
    {
        const int memberCount = 5;
        const int fanout = 2;

        var members = Enumerable
            .Range(0, memberCount)
            .Select(_ => new Member { Id = Guid.NewGuid().ToString("N") })
            .ToList();

        var environment = members.ToDictionary(
            m => m.Id,
            m => (
                Gossip: new Gossip.Gossip(m.Id, fanout, memberCount, null,
                    () => members.Select(x => x.Id).ToImmutableHashSet(), false),
                Member: m));

        var topology = new ClusterTopology
        {
            TopologyHash = Member.TopologyHash(members),
            Members = { members }
        };

        foreach (var m in environment.Values)
        {
            await m.Gossip.UpdateClusterTopology(topology.Clone());
        }

        const string stateKey = "data";
        const string stateValue = "value";

        foreach (var m in environment.Values)
        {
            m.Gossip.SetState(stateKey, new SomeGossipState { Key = stateValue });
        }

        var first = environment.Values.First().Gossip;

        var checkDefinition = Gossiper.ConsensusCheckBuilder<string>
            .Create<SomeGossipState>(stateKey, s => s.Key);

        var id = Guid.NewGuid().ToString();
        var (handle, check) = checkDefinition.Build(() => first.RemoveConsensusCheck(id));
        first.AddConsensusCheck(id, check);

        var ct = CancellationTokens.FromSeconds(10);

        _ = Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var m in environment.Values)
                {
                    foreach (var (member, delta) in m.Gossip.SendState())
                    {
                        var target = environment[member.Id];
                        target.Gossip.ReceiveState(delta.State);
                        delta.CommitOffsets();
                    }
                }
            }
        }, ct);

        var result = await handle.TryGetConsensus(ct);
        result.consensus.Should().BeTrue();
        result.value.Should().Be(stateValue);
    }

    [Fact]
    public async Task Gossip_should_replicate_large_state_with_small_batches()
    {
        const int memberCount = 5;
        const int fanout = 2;
        const int maxSend = 2;
        const int keysPerMember = 5;

        var members = Enumerable
            .Range(0, memberCount)
            .Select(_ => new Member { Id = Guid.NewGuid().ToString("N") })
            .ToList();

        var environment = members.ToDictionary(
            m => m.Id,
            m => (
                Gossip: new Gossip.Gossip(m.Id, fanout, maxSend, null,
                    () => members.Select(x => x.Id).ToImmutableHashSet(), false),
                Member: m));

        var topology = new ClusterTopology
        {
            TopologyHash = Member.TopologyHash(members),
            Members = { members }
        };

        foreach (var m in environment.Values)
        {
            await m.Gossip.UpdateClusterTopology(topology.Clone());
        }

        var expected = members.ToDictionary(m => m.Id, _ => new System.Collections.Generic.Dictionary<string, string>());

        foreach (var m in members.Select((member, index) => (member, index)))
        {
            for (var i = 0; i < keysPerMember; i++)
            {
                var key = $"k{m.index}-{i}";
                var value = $"v{m.index}-{i}";
                environment[m.member.Id].Gossip.SetState(key, new SomeGossipState { Key = value });
                expected[m.member.Id][key] = value;
            }
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var loop = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                foreach (var g in environment.Values)
                {
                    foreach (var (member, delta) in g.Gossip.SendState())
                    {
                        var target = environment[member.Id];
                        target.Gossip.ReceiveState(delta.State);
                        delta.CommitOffsets();
                    }
                }
            }
        }, cts.Token);

        while (!cts.IsCancellationRequested && !AllReplicated())
        {
            await Task.Delay(50, cts.Token);
        }

        cts.Cancel();
        await loop;

        AllReplicated().Should().BeTrue();

        bool AllReplicated()
        {
            var first = environment.Values.First();
            var snap = first.Gossip.GetStateSnapshot();
            if (snap.Members.Count != memberCount)
            {
                return false;
            }

            foreach (var (ownerId, kvs) in expected)
            {
                if (!snap.Members.TryGetValue(ownerId, out var ms) || ms.Values.Count < keysPerMember + 1)
                {
                    return false;
                }

                foreach (var (key, value) in kvs)
                {
                    if (!ms.Values.TryGetValue(key, out var any) ||
                        !any.Value.TryUnpack<SomeGossipState>(out var state) ||
                        state.Key != value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}