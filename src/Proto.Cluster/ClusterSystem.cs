using Proto.Remote;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Cluster
{
    public static class ClusterSystem
    {
        public static void Start(string clusterName, string address, IClusterProvider provider)
        {
            var (h, p) = ParseAddress(address);
            var kinds = Remote.Remote.GetKnownKinds();

            SubscribePartitionKindsToEventStream();
            SpawnPidCacheActor();
            SpawnMembershipActor();
            SubscribeMembershipActorToEventStream();
            provider.RegisterMember(clusterName, h, p, kinds);
            provider.MonitorMemberStatusChanges();
        }

        private static (string host,int port) ParseAddress(string address)
        {
            //TODO: use correct parsing
            var parts = address.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            return (host,port);
        }

        private static void SubscribeMembershipActorToEventStream()
        {
            throw new NotImplementedException();
        }

        private static void SpawnMembershipActor()
        {
            throw new NotImplementedException();
        }

        private static void SpawnPidCacheActor()
        {
            throw new NotImplementedException();
        }

        private static void SubscribePartitionKindsToEventStream()
        {
            throw new NotImplementedException();
        }
    }
}
