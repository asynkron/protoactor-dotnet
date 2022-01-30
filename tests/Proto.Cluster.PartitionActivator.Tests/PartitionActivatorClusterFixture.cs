// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Proto.Cluster.Identity;
using Proto.Cluster.Tests;

namespace Proto.Cluster.PartitionActivator.Tests
{
    public class PartitionActivatorClusterFixture : BaseInMemoryClusterFixture
    {
        public readonly ActorStateRepo Repository = new();

        public PartitionActivatorClusterFixture(
            int memberCount
        ) : base(memberCount)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionActivatorLookup(TimeSpan.FromSeconds(3));

        protected override ClusterKind[] ClusterKinds
            => new[] {new ClusterKind(ConcurrencyVerificationActor.Kind, Props.FromProducer(() => new ConcurrencyVerificationActor(Repository)))};
    }
}