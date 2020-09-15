// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
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
        private static ILogger _logger = null!;

        public Cluster(ActorSystem system, Serialization serialization)
        {
            System = system;
            Remote = new Remote.Remote(system, serialization);
        }

        public Guid Id { get; } = Guid.NewGuid();

        public ClusterConfig Config { get; private set; } = null!;

        public ActorSystem System { get; }

        public Remote.Remote Remote { get; }


        public MemberList MemberList { get; private set; } = null!;

        private IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Address;

        public Task StartMemberAsync(string clusterName, string address, int port, IClusterProvider cp)
            => StartMemberAsync(new ClusterConfig(clusterName, address, port, cp));

        public async Task StartMemberAsync(ClusterConfig config)
        {
            BeginStart(config,false);
            
            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;

            var kinds = Remote.GetKnownKinds();
            await Provider.StartMemberAsync(
                this,
                Config.Name,
                host,
                port,
                kinds,
                MemberList
            );

            _logger.LogInformation("Started as cluster member");
        }
        
        public async Task StartClientAsync(ClusterConfig config)
        {
            BeginStart(config,true);

            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;

            await Provider.StartClientAsync(
                this,
                Config.Name,
                host,
                port,
                MemberList
            );

            _logger.LogInformation("Started as cluster client");
        }

        private void BeginStart(ClusterConfig config, bool client)
        {
            Config = config;

            //default to partition identity lookup
            IdentityLookup = config.IdentityLookup ?? new PartitionIdentityLookup();
            Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);
            Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            _logger = Log.CreateLogger($"Cluster-{LoggerId}");
            _logger.LogInformation("Starting");
            MemberList = new MemberList(this);

            var kinds = Remote.GetKnownKinds();
            IdentityLookup.SetupAsync(this, kinds, client);
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            _logger.LogInformation("Stopping");
            if (graceful)
            {
                await IdentityLookup!.ShutdownAsync();
            }

            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);

            _logger.LogInformation("Stopped");
        }

        public Task<PID?> GetAsync(string identity, string kind) =>
            GetAsync(identity, kind, CancellationToken.None);

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct) => IdentityLookup!.GetAsync(identity, kind, ct);

        private ConcurrentDictionary<string,PID> _pidCache = new ConcurrentDictionary<string, PID>();
        public async Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct)
        {
            var key = kind + "." + identity;

            try
            {
                if (_pidCache.TryGetValue(key, out var cachedPid))
                {
                    var res = await System.Root.RequestAsync<T>(cachedPid, message, ct);
                    if (res != null)
                    {
                        return res;
                    }
                }
            }
            catch
            {
                //YOLO
            }
            finally
            {
                _pidCache.TryRemove(key,out _);
            }

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
                //update cache
                _pidCache[key] = pid;

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