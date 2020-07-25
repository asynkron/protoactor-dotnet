// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);

        internal ClusterConfig? Config { get; private set; }
        
        public ActorSystem System { get; }

        public Remote.Remote Remote { get; }

        public Cluster(ActorSystem system, Serialization serialization)
        {
            System = system;
            Remote = new Remote.Remote(system, serialization);
            Partition = new Partition(this);
            MemberList = new MemberList(this);
            
            PidCache = new PidCache();
            PidCacheUpdater = new PidCacheUpdater(this,PidCache);
        }

        internal Partition Partition { get; }
        internal MemberList MemberList { get; }
        internal PidCache PidCache { get; }
        internal PidCacheUpdater PidCacheUpdater { get; }
        
        private IIdentityLookup? IdentityLookup { get; set; }

        public Task Start(string clusterName, string address, int port, IClusterProvider cp)
            => Start(new ClusterConfig(clusterName, address, port, cp));

        public async Task Start(ClusterConfig config)
        {
            Config = config;

            //default to partition identity lookup
            IdentityLookup = config.IdentityLookup ?? new PartitionIdentityLookup();

            IdentityLookup.Setup(this);
            
            Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);

            Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            Logger.LogInformation("[Cluster] Starting...");

            var kinds = Remote.GetKnownKinds();

            Partition.Setup(kinds);
            if (config.UsePidCache)
            {
                PidCacheUpdater.Setup();
            }

            MemberList.Setup();

            var (host, port) = System.ProcessRegistry.GetAddress();

            await Config.ClusterProvider.StartAsync(
                this,
                Config.Name,
                host,
                port,
                kinds,
                Config.InitialMemberStatusValue,
                Config.MemberStatusValueSerializer
            );

            Logger.LogInformation("[Cluster] Started");
        }

        public async Task Shutdown(bool graceful = true)
        {
            Logger.LogInformation("[Cluster] Stopping...");
            if (graceful)
            {
                await Config!.ClusterProvider.ShutdownAsync(this);

                //This is to wait ownership transferring complete.
                await Task.Delay(2000);

                MemberList.Stop();
                PidCacheUpdater.Stop();
                Partition.Stop();
            }

            await Remote.Shutdown(graceful);

            Logger.LogInformation("[Cluster] Stopped");
        }

        public Task<(PID?, ResponseStatusCode)> GetAsync(string identity, string kind) => GetAsync(identity, kind, CancellationToken.None);

        public Task<(PID?, ResponseStatusCode)> GetAsync(string identity, string kind, CancellationToken ct)
        {
            if (Config.UsePidCache)
            {
                //Check Cache
                if (PidCache.TryGetCache(identity, out var pid)) 
                    return Task.FromResult((pid, ResponseStatusCode.OK));
            }

            return IdentityLookup.GetAsync(identity, kind, ct);
        }
    }
}
