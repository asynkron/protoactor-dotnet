namespace Proto.Cluster
{
    public static class Cluster
    {
        public static void Start(string clusterName, string address, IClusterProvider provider)
        {
            var (h, p) = ParseAddress(address);
            var kinds = Remote.Remote.GetKnownKinds();

            Partition.SubscribeToEventStream();
            PidCache.Spawn();
            MemberList.Spawn();
            MemberList.SubscribeToEventStream();
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
    }
}