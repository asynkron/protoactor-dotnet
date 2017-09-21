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

        private static IClusterProvider cp;
        
        public static void Start(string clusterName, string address, int port, IClusterProvider provider)
        {
            Remote.Remote.Start(address, port);

            cp = provider;
            
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
            cp.RegisterMemberAsync(clusterName, h, p, kinds).Wait();
            cp.MonitorMemberStatusChanges();

            Logger.LogInformation("Started Cluster");
        }
        
        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                cp.Shutdown();

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

        public static Task<ActorPidResponse> GetAsync(string name, string kind) => GetAsync(name, kind, CancellationToken.None);

        public static async Task<ActorPidResponse> GetAsync(string name, string kind, CancellationToken ct)
        {
            var req = new PidCacheRequest(name, kind);
            var res = await PidCache.Pid.RequestAsync<ActorPidResponse>(req, ct);
            return res;
        }

        public static void RemoveCache(string name)
        {
            var req = new RemoveCachedPidRequest(name);
            PidCache.Pid.Tell(req);
        }
    }
}