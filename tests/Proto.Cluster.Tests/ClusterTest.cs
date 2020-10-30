namespace Proto.Cluster.Tests
{
    using System;
    using System.Collections.Immutable;
    using System.Collections.Generic;
    using System.Linq;



    public abstract class ClusterTest
    {
        private readonly IClusterFixture _clusterFixture;
        private readonly string _runId;

        protected ImmutableList<Cluster> Members => _clusterFixture.Members;


        protected ClusterTest(IClusterFixture clusterFixture)
        {
            _clusterFixture = clusterFixture;
            _runId = Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>
        /// To be able to re-use the fixture across tests, we can make sure the other test identities do not collide
        /// </summary>
        /// <param name="baseId"></param>
        /// <returns></returns>
        protected string CreateIdentity(string baseId)
        {
            return $"{_runId}-{baseId}";
        }

        
        protected IEnumerable<string> GetActorIds(int count) =>
            Enumerable.Range(1, count)
                .Select(i => CreateIdentity(i.ToString()));
    }
}