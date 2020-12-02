using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Proto.Cluster.Tests
{
    [Collection("ClusterTests")]
    public abstract class ClusterTestBase
    {
        private readonly IClusterFixture _clusterFixture;
        private readonly string _runId;

        protected ClusterTestBase(IClusterFixture clusterFixture)
        {
            _clusterFixture = clusterFixture;
            _runId = Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        protected ImmutableList<Cluster> Members => _clusterFixture.Members;

        /// <summary>
        ///     To be able to re-use the fixture across tests, we can make sure the other test identities do not collide
        /// </summary>
        /// <param name="baseId"></param>
        /// <returns></returns>
        protected string CreateIdentity(string baseId) => $"{_runId}-{baseId}-";

        protected IEnumerable<string> GetActorIds(int count) =>
            Enumerable.Range(1, count)
                .Select(i => CreateIdentity(i.ToString()));
    }
}