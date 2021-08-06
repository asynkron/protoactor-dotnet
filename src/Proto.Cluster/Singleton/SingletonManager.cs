// -----------------------------------------------------------------------
// <copyright file="SingletonManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Singleton
{
    public class SingletonManager
    {
        private readonly Cluster _cluster;

        public SingletonManager(Cluster cluster)
        {
            _cluster = cluster;
            cluster.System.EventStream.Subscribe<GossipUpdate>(g => {
                    

                }
            );
        }

        public void Track(ClusterIdentity identity)
        {
            
        }

        public void Untrack(ClusterIdentity identity)
        {
            
        }
    }
}