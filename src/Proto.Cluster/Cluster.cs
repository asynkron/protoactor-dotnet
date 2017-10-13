// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public class ClusterConfig
    {
        public string Name { get; }
        public string Address { get; }
        public int Port { get; }
        public int Weight { get; private set; }
        public IClusterProvider Provider { get; }

        public ClusterConfig(string name, string address, int port, int weight, IClusterProvider provider)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
            Weight = weight;
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public void UpdateWeight(int weight)
        {
            Weight = weight;
        }
    }

    public static class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);

        private static ClusterConfig cfg;

        public static void Start(string clusterName, string address, int port, IClusterProvider provider)
            => StartWithConfig(new ClusterConfig(clusterName, address, port, 5, provider));

        public static void StartWithConfig(ClusterConfig config)
        {
            if (config.Weight > 10)
            {
                Logger.LogError("Currently cluster only support maximum weight of 10");
                config.UpdateWeight(10);
            }

            cfg = config;

            Remote.Remote.Start(cfg.Address, cfg.Port);

            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Logger.LogInformation("Starting Proto.Actor cluster");
            var (h, p) = ParseAddress(ProcessRegistry.Instance.Address);
            var kinds = Remote.Remote.GetKnownKinds();
            Partition.SpawnPartitionActors(kinds);
            Partition.SubscribeToEventStream();
            PidCache.Spawn();
            PidCache.SubscribeToEventStream();
            MemberList.Spawn();
            MemberList.SubscribeToEventStream();
            cfg.Provider.RegisterMemberAsync(cfg.Name, h, p, cfg.Weight, kinds).Wait();
            cfg.Provider.MonitorMemberStatusChanges();

            Logger.LogInformation("Started Cluster");
        }
        
        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                cfg.Provider.Shutdown();
                //This is to wait ownership transfering complete.
                Task.Delay(2000).Wait();
                MemberList.UnsubEventStream();
                MemberList.Stop();
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

        public static void UpdateWeight(int weight)
        {
            if (weight > 10)
            {
                Logger.LogError("Currently cluster only support maximum weight of 10");
                weight = 10;
            }
            cfg.UpdateWeight(weight);
            cfg.Provider.UpdateWeight(weight);
        }

        public static Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind)
            => GetAsync(name, kind, CancellationToken.None);

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