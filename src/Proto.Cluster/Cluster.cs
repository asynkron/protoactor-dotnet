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

        public static Task Start(string clusterName, string address, int port, IClusterProvider cp)
            => Start(new ClusterConfig(clusterName, address, port, cp));

        public static async Task Start(ClusterConfig config)
        {
            Config = config;

            Remote.Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);

            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            Logger.LogInformation("Starting Proto.Actor cluster");

            var kinds = Remote.Remote.GetKnownKinds();
            Partition.Setup(kinds);
            PidCache.Setup();
            MemberList.Setup();

            var (host, port) = ProcessRegistry.Instance.GetAddress();

            await Config.ClusterProvider.RegisterMemberAsync(
                Config.Name, host, port, kinds, Config.InitialMemberStatusValue, Config.MemberStatusValueSerializer
            );
            Config.ClusterProvider.MonitorMemberStatusChanges();

            Logger.LogInformation("Started cluster");
        }

        public static async Task Shutdown(bool graceful = true)
        {
            if (graceful)
            {
                await Config.ClusterProvider.Shutdown();

                //This is to wait ownership transferring complete.
                await Task.Delay(2000);

                MemberList.Stop();
                PidCache.Stop();
                Partition.Stop();
            }

            await Remote.Remote.Shutdown(graceful);

            Logger.LogInformation("Stopped Cluster");
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

            Logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", address, remotePid, req);
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
            catch (TimeoutException e)
            {
                Logger.LogWarning(e, "Remote PID request timeout {@Request}", req);
                return (null, ResponseStatusCode.Timeout);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occured requesting remote PID {@Request}", req);
                return (null, ResponseStatusCode.Error);
            }
        }
    }
}