// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);

        internal static ClusterConfig cfg;

        public static void Start(string clusterName, string address, int port, IClusterProvider cp) => StartWithConfig(new ClusterConfig(clusterName, address, port, cp));

        public static void StartWithConfig(ClusterConfig config)
        {
            cfg = config;

            Remote.Remote.Start(cfg.Address, cfg.Port, cfg.RemoteConfig);
        
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Logger.LogInformation("Starting Proto.Actor cluster");
            var (h, p) = ParseAddress(ProcessRegistry.Instance.Address);
            var kinds = Remote.Remote.GetKnownKinds();
            Partition.SpawnPartitionActors(kinds);
            Partition.SubscribeToEventStream();
            PidCache.Spawn();
            PidCache.SubscribeToEventStream();
            MemberList.SubscribeToEventStream();
            cfg.ClusterProvider.RegisterMemberAsync(cfg.Name, h, p, kinds, config.InitialMemberStatusValue, config.MemberStatusValueSerializer).Wait();
            cfg.ClusterProvider.MonitorMemberStatusChanges();

            Logger.LogInformation("Started Cluster");
        }

        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                cfg.ClusterProvider.Shutdown();
                //This is to wait ownership transfering complete.
                Task.Delay(2000).Wait();
                MemberList.UnsubEventStream();
                PidCache.UnsubEventStream();
                PidCache.Stop();
                Partition.UnsubEventStream();
                Partition.StopPartitionActors();
            }

            Remote.Remote.Shutdown(gracefull);

            Logger.LogInformation("Stopped Cluster");
        }

        private static (string host, int port) ParseAddress(string address)
        {
            //TODO: use correct parsing
            var parts = address.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            return (host, port);
        }

        public static Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind) => GetAsync(name, kind, CancellationToken.None);

        public static async Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind, CancellationToken ct)
        {
            var req = new PidCacheRequest(name, kind);
            var resp = await PidCache.Pid.RequestAsync<PidCacheResponse>(req, ct);
            return (resp.Pid, resp.StatusCode);
        }

        public static void RemoveCache(string name)
        {
            var req = new RemovePidCacheRequest(name);
            PidCache.Pid.Tell(req);
        }
    }
}