// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class Cluster
    {
        private static ILogger _logger = Log.CreateLogger(typeof(Cluster).FullName);

        public static void Start(string clusterName, IClusterProvider provider)
        {
            _logger.LogInformation("Starting Proto.Actor cluster");
            var (h, p) = ParseAddress(ProcessRegistry.Instance.Address);
            var kinds = Remote.Remote.GetKnownKinds();
            Partition.SpawnPartitionActors(kinds);
            Partition.SubscribeToEventStream();
            PidCache.Spawn();
            MemberList.Spawn();
            MemberList.SubscribeToEventStream();
            provider.RegisterMemberAsync(clusterName, h, p, kinds).Wait();
            provider.MonitorMemberStatusChanges();
            _logger.LogInformation("Cluster started");
        }

        private static (string host, int port) ParseAddress(string address)
        {
            //TODO: use correct parsing
            var parts = address.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            return (host, port);
        }

        public static async Task<PID> GetAsync(string name, string kind)
        {
            var req = new PidCacheRequest(name, kind);
            var res = await PidCache.Pid.RequestAsync<ActorPidResponse>(req);
            return res.Pid;
        }
    }
}