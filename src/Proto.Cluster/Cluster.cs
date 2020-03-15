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
    public class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);

        internal ClusterConfig Config;
        public ActorSystem System
        {
            get;
        }

        public Remote.Remote Remote
        {
            get;
        }

        public Cluster(ActorSystem system, Serialization serialization)
        {
            System = system;
            Remote = new Remote.Remote(system, serialization);
            Partition = new Partition(this);
            MemberList = new MemberList(this);
            PidCache = new PidCache(this);
        }
        internal Partition Partition { get; }
        internal MemberList MemberList { get; }
        internal PidCache PidCache { get; }

        public Task Start(string clusterName, string address, int port, IClusterProvider cp)
            => Start(new ClusterConfig(clusterName, address, port, cp));

        public async Task Start(ClusterConfig config)
        {
            Config = config;

            this.Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);

            this.Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            Logger.LogInformation("Starting Proto.Actor cluster");

            var kinds = this.Remote.GetKnownKinds();

            Partition.Setup(kinds);
            PidCache.Setup();
            MemberList.Setup();

            var (host, port) = System.ProcessRegistry.GetAddress();

            await Config.ClusterProvider.RegisterMemberAsync(this, Config.Name, host, port, kinds, Config.InitialMemberStatusValue, Config.MemberStatusValueSerializer
            );
            Config.ClusterProvider.MonitorMemberStatusChanges(this);

            Logger.LogInformation("Started cluster");
        }

        public async Task Shutdown(bool graceful = true)
        {
            if (graceful)
            {
                await Config.ClusterProvider.Shutdown(this);

                //This is to wait ownership transferring complete.
                await Task.Delay(2000);

                MemberList.Stop();
                PidCache.Stop();
                Partition.Stop();
            }

            await Remote.Shutdown(graceful);

            Logger.LogInformation("Stopped Cluster");
        }

        public Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind) => GetAsync(name, kind, CancellationToken.None);

        public async Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind, CancellationToken ct)
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
                    ? await System.Root.RequestAsync<ActorPidResponse>(remotePid, req, Config.TimeoutTimespan)
                    : await System.Root.RequestAsync<ActorPidResponse>(remotePid, req, ct);
                var status = (ResponseStatusCode)resp.StatusCode;

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