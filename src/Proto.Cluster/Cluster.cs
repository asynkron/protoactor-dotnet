using System;

namespace Proto.Cluster
{
    public static class Cluster
    {
        private static readonly Random Random = new Random();

        public static void Start(string clusterName, string address, IClusterProvider provider)
        {
            var (h, p) = ParseAddress(address);
            var kinds = Remote.Remote.GetKnownKinds();

            Partition.SubscribePartitionKindsToEventStream();
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
            return (host, port);
        }

        public static string GetRandomActivator(string kind)
        {
            var r = Random.Next();
            var members = GetMembers(kind);
            return members[r % members.Length];
        }

        private static string[] GetMembers(string kind)
        {
            throw new NotImplementedException();
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
    }
}