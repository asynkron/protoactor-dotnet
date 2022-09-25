// -----------------------------------------------------------------------
// <copyright file="HandoverTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Proto.Cluster.Partition;
using Xunit;

namespace Proto.Cluster.PartitionIdentity.Tests;

public class HandoverSinkTests
{
    private const string TestKind = "some-kind";

    private static readonly Member TestMember1 = new()
    {
        Host = "127.0.0.1",
        Kinds = { TestKind },
        Id = Guid.NewGuid().ToString(),
        Port = 50000
    };

    private static readonly Member TestMember2 = new()
    {
        Host = "127.0.0.1",
        Kinds = { TestKind },
        Id = Guid.NewGuid().ToString(),
        Port = 50001
    };

    private int _counter;

    [Fact]
    public void CompletesOnEmptyHandover()
    {
        var topology = CreateTopology(TestMember1, TestMember2);
        var receivedCount = 0;
        var sink = new HandoverSink(topology, handover => receivedCount += handover.Actors.Count);

        var completeAfterFirst = sink.Receive(TestMember1.Address, EmptyFinalHandover(topology));
        var completeAfterSecond = sink.Receive(TestMember2.Address, EmptyFinalHandover(topology));

        completeAfterFirst.Should().BeFalse("There are two members, this was the first of them");
        completeAfterSecond.Should().BeTrue("Handovers were received from both members");
        receivedCount.Should().Be(0, "No activations were sent");
    }

    [Fact]
    public void CompletesOnNonChunkedHandover()
    {
        var topology = CreateTopology(TestMember1, TestMember2);
        var receivedCount = 0;
        var sink = new HandoverSink(topology, handover => receivedCount += handover.Actors.Count);
        var activationsPerNode = 10;

        var completeAfterFirst =
            sink.Receive(TestMember1.Address, CreateHandover(topology, TestMember1, activationsPerNode));

        completeAfterFirst.Should().BeFalse("There are two members, this was the first of them");
        receivedCount.Should().Be(activationsPerNode);

        var completeAfterSecond =
            sink.Receive(TestMember2.Address, CreateHandover(topology, TestMember2, activationsPerNode));

        completeAfterSecond.Should().BeTrue("Handovers were received from both members");
        receivedCount.Should().Be(activationsPerNode * topology.Members.Count);
    }

    [Fact]
    public void CompletesOnChunkedHandover()
    {
        var topology = CreateTopology(TestMember1, TestMember2);
        var receivedCount = 0;
        var sink = new HandoverSink(topology, handover => receivedCount += handover.Actors.Count);
        var activationsPerMember = 50;

        var activator1 = TestMember1.Address;

        foreach (var handover in CreateHandovers(topology, TestMember1, 50))
        {
            sink.Receive(activator1, handover);
        }

        sink.IsComplete.Should().BeFalse("There are two members, this was the first of them");
        receivedCount.Should().Be(activationsPerMember);

        var activator2 = TestMember2.Address;

        foreach (var handover in CreateHandovers(topology, TestMember2, 50))
        {
            sink.Receive(activator2, handover);
        }

        sink.IsComplete.Should().BeTrue("Handovers were received from both members");
        receivedCount.Should().Be(activationsPerMember * topology.Members.Count);
    }

    [Fact]
    public void DoesNotCompleteWhenMissingChunk()
    {
        var topology = CreateTopology(TestMember1, TestMember2);
        var receivedCount = 0;
        var sink = new HandoverSink(topology, handover => receivedCount += handover.Actors.Count);
        var activationsPerMember = 50;
        var chunkSize = 15;

        var activator1 = TestMember1.Address;

        var i = 0;

        foreach (var handover in CreateHandovers(topology, TestMember1, 50, chunkSize))
        {
            if (++i == 3)
            {
                continue;
            }

            sink.Receive(activator1, handover);
        }

        var activator2 = TestMember2.Address;

        foreach (var handover in CreateHandovers(topology, TestMember2, 50, chunkSize))
        {
            sink.Receive(activator2, handover);
        }

        sink.IsComplete.Should().BeFalse("Handover is missing a message");
        receivedCount.Should().Be(activationsPerMember * topology.Members.Count - chunkSize);
    }

    [Fact]
    public void CompletesWithOutOfOrderChunks()
    {
        var topology = CreateTopology(TestMember1, TestMember2);
        var receivedCount = 0;
        var sink = new HandoverSink(topology, handover => receivedCount += handover.Actors.Count);
        var activationsPerMember = 50;
        var chunkSize = 15;

        var activator1 = TestMember1.Address;

        foreach (var handover in CreateHandovers(topology, TestMember1, 50, chunkSize)
                     .OrderBy(it => it.Actors.First().Identity)) // Randomized order
        {
            sink.Receive(activator1, handover);
        }

        var activator2 = TestMember2.Address;

        foreach (var handover in CreateHandovers(topology, TestMember2, 50, chunkSize)
                     .OrderBy(it => it.Actors.First().Identity))
        {
            sink.Receive(activator2, handover);
        }

        sink.IsComplete.Should().BeTrue("Order should not affect result");
        receivedCount.Should().Be(activationsPerMember * topology.Members.Count);
    }

    [Fact]
    public void DuplicatesAreFiltered()
    {
        var topology = CreateTopology(TestMember1);
        var receivedCount = 0;
        var duplicateCount = 0;

        var sink = new HandoverSink(topology,
            handover => receivedCount += handover.Actors.Count,
            duplicateHandover => duplicateCount += duplicateHandover.Actors.Count
        );

        var activationsPerMember = 50;
        var chunkSize = 15;

        var activator1 = TestMember1.Address;

        var identityHandovers = CreateHandovers(topology, TestMember1, 50, chunkSize).ToList();

        foreach (var handover in identityHandovers)
        {
            sink.Receive(activator1, handover);
            sink.Receive(activator1, handover);
        }

        sink.IsComplete.Should().BeTrue("Duplication should not affect result");
        receivedCount.Should().Be(activationsPerMember * topology.Members.Count);
        duplicateCount.Should().Be(activationsPerMember * topology.Members.Count);
    }

    private static ClusterTopology CreateTopology(params Member[] members)
    {
        var memberSet = new ImmutableMemberSet(members);

        return new ClusterTopology
        {
            TopologyHash = memberSet.TopologyHash,
            Members = { memberSet.Members },
            TopologyValidityToken = CancellationToken.None
        };
    }

    private IdentityHandover CreateHandover(ClusterTopology topology, Member member, int activations) =>
        CreateHandovers(topology, member, activations, activations).Single();

    private IEnumerable<IdentityHandover> CreateHandovers(ClusterTopology topology, Member member, int activations,
        int chunkSize = 10)
    {
        var remaining = activations;
        var chunkId = 0;

        while (remaining > chunkSize)
        {
            yield return new IdentityHandover
            {
                TopologyHash = topology.TopologyHash,
                ChunkId = ++chunkId,
                Actors = { CreateActivations(member.Address, chunkSize) }
            };

            remaining -= chunkSize;
        }

        yield return new IdentityHandover
        {
            Final = true,
            Sent = activations,
            Skipped = 0,
            TopologyHash = topology.TopologyHash,
            ChunkId = ++chunkId,
            Actors = { CreateActivations(member.Address, remaining) }
        };
    }

    private IEnumerable<Activation> CreateActivations(string address, int count) =>
        Enumerable.Range(0, count)
            .Select(i =>
                {
                    var identity = Guid.NewGuid().ToString("N");

                    return new Activation
                    {
                        ClusterIdentity = ClusterIdentity.Create(identity, TestKind),
                        Pid = PID.FromAddress(address, $"partition-activator$99/{identity}${++_counter}")
                    };
                }
            );

    private static IdentityHandover EmptyFinalHandover(ClusterTopology topology) =>
        new()
        {
            Final = true,
            TopologyHash = topology.TopologyHash,
            ChunkId = 1
        };
}