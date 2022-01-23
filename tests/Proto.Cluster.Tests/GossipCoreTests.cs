// -----------------------------------------------------------------------
// <copyright file="GossipCoreTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class GossipCoreTests
    {
        [Fact]
        public void Foo()
        {
            const int memberCount = 10;
            const int fanout = 3;

            var members =
                Enumerable
                    .Range(0, memberCount)
                    .Select(_ => Guid.NewGuid().ToString("N"))
                    .ToDictionary(
                        id => id, 
                        id => new Gossip.Gossip(id, fanout, () => ImmutableHashSet<string>.Empty, null));
         
            Action<MemberStateDelta, Member, InstanceLogger> sendState = (memberStateDelta, receiver,_) => {
                    var otherGossip = members[receiver.Id];
                    otherGossip.ReceiveState(memberStateDelta.State);
                    memberStateDelta.CommitOffsets();
                };
        }
    }
}