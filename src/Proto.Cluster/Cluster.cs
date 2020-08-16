// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster
    {
        public Guid Id { get; } = Guid.NewGuid();

        private static ILogger _logger = null!;

        internal ClusterConfig Config { get; private set; } = null!;
        
        public ActorSystem System { get; }

        public Remote.Remote Remote { get; }

        public Cluster(ActorSystem system, Serialization serialization)
        {
            _logger = Log.CreateLogger($"Cluster-{Id}");
            System = system;
            Remote = new Remote.Remote(system, serialization);

            PidCache = new PidCache();
            MemberList = new MemberList(this);
            PidCacheUpdater = new PidCacheUpdater(this,PidCache);
        }


        internal MemberList MemberList { get; }
        internal PidCache PidCache { get; }
        internal PidCacheUpdater PidCacheUpdater { get; }
        
        private IIdentityLookup? IdentityLookup { get; set; }

        public Task StartAsync(string clusterName, string address, int port, IClusterProvider cp)
            => StartAsync(new ClusterConfig(clusterName, address, port, cp));

        public async Task StartAsync(ClusterConfig config)
        {
            Config = config;

            //default to partition identity lookup
            IdentityLookup = config.IdentityLookup ?? new PartitionIdentityLookup();
            
            Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);

            Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            _logger.LogInformation("Starting");

            var kinds = Remote.GetKnownKinds();
            IdentityLookup.Setup(this, kinds);
            


            if (config.UsePidCache)
            {
                PidCacheUpdater.Setup();
            }

            var (host, port) = System.ProcessRegistry.GetAddress();

            await Config.ClusterProvider.StartAsync(
                this,
                Config.Name,
                host,
                port,
                kinds,
                MemberList
            );

            _logger.LogInformation("Started");
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            _logger.LogInformation("Stopping");
            if (graceful)
            {
                PidCacheUpdater!.Shutdown();
                IdentityLookup!.Shutdown();
            }
            
            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);
            
            _logger.LogInformation("Stopped");
        }

        public Task<(PID?, ResponseStatusCode)> GetAsync(string identity, string kind) => GetAsync(identity, kind, CancellationToken.None);

        public Task<(PID?, ResponseStatusCode)> GetAsync(string identity, string kind, CancellationToken ct)
        {
            if (Config.UsePidCache)
            {
                //Check Cache
                if (PidCache.TryGetCache(identity, out var pid)) 
                    return Task.FromResult<(PID?, ResponseStatusCode)>((pid, ResponseStatusCode.OK));
            }

            return IdentityLookup!.GetAsync(identity, kind, ct);
        }
    }
}
