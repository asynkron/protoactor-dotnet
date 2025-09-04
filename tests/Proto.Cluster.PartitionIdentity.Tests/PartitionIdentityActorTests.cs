// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityActorTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using FluentAssertions;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Xunit;

namespace Proto.Cluster.PartitionIdentity.Tests;

public class PartitionIdentityActorTests
{
    [Fact]
    public void IsTopologyValid_returns_false_when_hash_mismatch()
    {
        var method = typeof(PartitionIdentityActor)
            .GetMethod("IsTopologyValid", BindingFlags.NonPublic | BindingFlags.Static)!;

        var msg = new ClusterTopology { TopologyHash = 2 };
        var result = (bool)method.Invoke(null, new object[] { 1UL, msg, CancellationToken.None })!;

        result.Should().BeFalse();
    }

    [Fact]
    public void Rebalance_invokes_partition_pull()
    {
        var actor = TestPartitionIdentityActor.Create();
        var members = new[]
        {
            new Member { Host = "a", Port = 1 },
            new Member { Host = "b", Port = 2 }
        };

        var topology = new ClusterTopology { TopologyHash = 1 };
        topology.Members.AddRange(members);

        actor.InvokeRebalance(topology, true, 1, TimeSpan.Zero);

        actor.StartPartitionPullCalled.Should().BeTrue();
        actor.CapturedAddresses.Should().BeEquivalentTo(members.Select(m => m.Address));
    }

    private class TestPartitionIdentityActor : PartitionIdentityActor
    {
        private TestPartitionIdentityActor() : base(null!, new PartitionConfig())
        {
        }

        public bool StartPartitionPullCalled { get; private set; }
        public string[] CapturedAddresses { get; private set; } = Array.Empty<string>();

        public static TestPartitionIdentityActor Create()
        {
            var actor = (TestPartitionIdentityActor)FormatterServices
                .GetUninitializedObject(typeof(TestPartitionIdentityActor));

            var cluster = (Cluster)FormatterServices.GetUninitializedObject(typeof(Cluster));
            var system = new ActorSystem();
            typeof(Cluster).GetField("<System>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cluster, system);
            typeof(PartitionIdentityActor).GetField("_cluster", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(actor, cluster);
            typeof(PartitionIdentityActor).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(actor, new PartitionConfig());
            typeof(PartitionIdentityActor).GetField("_myAddress", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(actor, "test");
            typeof(PartitionIdentityActor).GetField("_currentTopology", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(actor, new ClusterTopology { TopologyHash = 1 });

            return actor;
        }

        protected override void StartPartitionPull(ClusterTopology msg, IEnumerable<string> memberAddresses, IContext context,
            ClusterTopology? deltaBaseline = null)
        {
            StartPartitionPullCalled = true;
            CapturedAddresses = memberAddresses.ToArray();
        }

        public void InvokeRebalance(ClusterTopology msg, bool allNodesCompleted, ulong consensusHash, TimeSpan duration)
        {
            var method = typeof(PartitionIdentityActor)
                .GetMethod("Rebalance", BindingFlags.NonPublic | BindingFlags.Instance)!;
            method.Invoke(this, new object[] { msg, allNodesCompleted, consensusHash, duration, null! });
        }
    }
}
