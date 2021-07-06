// -----------------------------------------------------------------------
// <copyright file="PidCacheInvalidationTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class PidCacheInvalidationTests : IClassFixture<InMemoryPidCacheInvalidationClusterFixture>
    {
        private InMemoryPidCacheInvalidationClusterFixture ClusterFixture { get; }

        private IList<Cluster> Members => ClusterFixture.Members;

        public PidCacheInvalidationTests(InMemoryPidCacheInvalidationClusterFixture clusterFixture) => ClusterFixture = clusterFixture;

        [Fact]
        public async Task PidCacheInvalidatesCorrectly()
        {
            const string id = "1";

            var remoteMember = await GetRemoteMemberFromActivation(id);
            var cachedPid = GetFromPidCache(remoteMember, id);

            cachedPid.Should().NotBeNull();
            await remoteMember.RequestAsync<object>(id, EchoActor.Kind, new Die(), CancellationToken.None);

            await Task.Delay(1000); // PidCache is asynchronously cleared, allow the system to purge it

            var cachedPidAfterStopping = GetFromPidCache(remoteMember, id);

            cachedPidAfterStopping.Should().BeNull();
        }

        private static PID GetFromPidCache(Cluster remoteMember, string id)
        {
            remoteMember.PidCache.TryGet(new ClusterIdentity
                {
                    Identity = id,
                    Kind = EchoActor.Kind
                }, out var activation
            );
            return activation;
        }

        private async Task<Cluster> GetRemoteMemberFromActivation(string id)
        {
            foreach (var member in Members)
            {
                var response = await member.RequestAsync<HereIAm>(id, EchoActor.Kind, new WhereAreYou(), CancellationToken.None);

                // Get the first member which does not have the activation local to it.
                if (!response.Address.Equals(member.System.Address, StringComparison.OrdinalIgnoreCase)) return member;
            }

            throw new Exception("Something wrong here..");
        }
    }
}