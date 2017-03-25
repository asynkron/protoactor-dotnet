// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    public static class Cluster
    {
        internal static ILogger Logger { get; } = Log.CreateLogger("Cluster");

        public static void Start(string clusterName, string address, IClusterProvider provider)
        {
            Logger.LogInformation("Starting Proto.Actor cluster");
            var (h, p) = ParseAddress(address);
            var kinds = Remote.Remote.GetKnownKinds();
            Partition.SpawnPartitionActors(kinds);
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