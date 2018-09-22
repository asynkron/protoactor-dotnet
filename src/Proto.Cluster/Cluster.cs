// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);

        internal static ClusterConfig Config;

        public static void Start(string clusterName, string address, int port, IClusterProvider cp) => StartWithConfig(new ClusterConfig(clusterName, address, port, cp));

        public static void StartWithConfig(ClusterConfig config)
        {
            Config = config;

            Remote.Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);
        
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Logger.LogInformation("Starting Proto.Actor cluster");
            var (host, port) = ParseAddress(ProcessRegistry.Instance.Address);
            var kinds = Remote.Remote.GetKnownKinds();
            Partition.Setup(kinds);
            PidCache.Setup();
            MemberList.Setup();
            Config.ClusterProvider.RegisterMemberAsync(Config.Name, host, port, kinds, config.InitialMemberStatusValue, config.MemberStatusValueSerializer).Wait();
            Config.ClusterProvider.MonitorMemberStatusChanges();

            Logger.LogInformation("Started Cluster");
        }

        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                Config.ClusterProvider.Shutdown();
                //This is to wait ownership transfering complete.
                Task.Delay(2000).Wait();
                MemberList.Stop();
                PidCache.Stop();
                Partition.Stop();
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
            //Check Cache
            if (PidCache.TryGetCache(name, out var pid))
                return (pid, ResponseStatusCode.OK);

            //Get Pid
            var address = MemberList.GetPartition(name, kind);

            if (string.IsNullOrEmpty(address))
            {
                return (null, ResponseStatusCode.Unavailable);
            }

            var remotePid = Partition.PartitionForKind(address, kind);
            var req = new ActorPidRequest
            {
                Kind = kind,
                Name = name
            };

            try
            {
                var resp = ct == CancellationToken.None
                           ? await RootContext.Empty.RequestAsync<ActorPidResponse>(remotePid, req, Config.TimeoutTimespan)
                           : await RootContext.Empty.RequestAsync<ActorPidResponse>(remotePid, req, ct);
                var status = (ResponseStatusCode) resp.StatusCode;
                switch (status)
                {
                    case ResponseStatusCode.OK:
                        PidCache.TryAddCache(name, resp.Pid);
                        return (resp.Pid, status);
                    default:
                        return (resp.Pid, status);
                }
            }
            catch(TimeoutException)
            {
                return (null, ResponseStatusCode.Timeout);
            }
            catch
            {
                return (null, ResponseStatusCode.Error);
            }
        }

        public static void RemoveCache(string name) => PidCache.RemoveCacheByName(name);
    }
}