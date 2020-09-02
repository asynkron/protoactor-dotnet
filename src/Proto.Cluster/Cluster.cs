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
    public class Cluster : IProtoPlugin
    {
        private static ILogger _logger = null!;

        public Cluster(ActorSystem system, ClusterConfig clusterConfig)
        {
            system.Plugins.AddPlugin(this);
            Config = clusterConfig;
            System = system;
            Remote = system.Plugins.GetPlugin<IRemote>();
            PidCache = new PidCache();
            PidCacheUpdater = new PidCacheUpdater(this, PidCache);
            //default to partition identity lookup
            IdentityLookup = clusterConfig.IdentityLookup ?? new PartitionIdentityLookup();
            Provider = clusterConfig.ClusterProvider;
            Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }

        public Cluster(ActorSystem system, string clusterName, IClusterProvider cp)
            : this(system, new ClusterConfig(clusterName, cp))
        {
        }

        public Guid Id { get; } = Guid.NewGuid();

        internal ClusterConfig Config { get; }

        public ActorSystem System { get; }

        public IRemote Remote { get; }


        internal MemberList? MemberList { get; private set; }
        internal PidCache PidCache { get; }
        internal PidCacheUpdater PidCacheUpdater { get; }

        private IIdentityLookup IdentityLookup { get; }

        internal IClusterProvider Provider { get; set; }

        public string LoggerId => System.ProcessRegistry.Address;

        public async Task StartAsync()
        {
            Remote.Start();
            _logger = Log.CreateLogger($"Cluster-{LoggerId}");
            _logger.LogInformation("Starting");
            MemberList = new MemberList(this);

            var (host, port) = System.ProcessRegistry.GetAddress();
            var kinds = Remote.RemoteKindRegistry.GetKnownKinds();
            IdentityLookup.Setup(this, kinds);
            if (Config.UsePidCache)
            {
                PidCacheUpdater.Setup();
            }
            await Provider.StartAsync(
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
                if (Config.UsePidCache)
                {
                    PidCacheUpdater.Shutdown();
                }

                IdentityLookup!.Shutdown();
            }

            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Task.Delay(500);
            await Remote.ShutdownAsync(graceful);

            _logger.LogInformation("Stopped");
        }

        public Task<PID?> GetAsync(string identity, string kind) =>
            GetAsync(identity, kind, CancellationToken.None);

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct)
        {
            if (Config.UsePidCache)
            {
                //Check Cache
                if (PidCache.TryGetCache(identity, out var pid))
                {
                    return Task.FromResult<PID?>(pid);
                }
            }

            return IdentityLookup!.GetAsync(identity, kind, ct);
        }

        public async Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct)
        {
            var i = 0;
            while (!ct.IsCancellationRequested)
            {
                var delay = i * 20;
                i++;
                var pid = await GetAsync(identity, kind, ct);
                if (pid == null)
                {
                    await Task.Delay(delay, CancellationToken.None);
                    continue;
                }

                var res = await System.Root.RequestAsync<T>(pid, message, ct);
                if (res == null)
                {
                    await Task.Delay(delay, CancellationToken.None);
                    continue;
                }

                return res;
            }

            return default!;
        }
    }
}