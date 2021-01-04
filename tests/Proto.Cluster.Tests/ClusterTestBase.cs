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
        protected readonly IClusterFixture ClusterFixture;
        private readonly string _runId;

        protected ClusterTestBase(IClusterFixture clusterFixture)
        {
            ClusterFixture = clusterFixture;
            _runId = Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        protected IList<Cluster> Members => ClusterFixture.Members;

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