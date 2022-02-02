// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;

namespace Proto.Cluster.Partition
{
    public class PartitionIdentityLookup : IIdentityLookup
    {
        private Cluster _cluster = null!;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityLookup>();
        private PartitionManager _partitionManager = null!;
        private readonly TimeSpan _getPidTimeout;
        private readonly PartitionConfig _config;

        public PartitionIdentityLookup(TimeSpan identityHandoverTimeout, TimeSpan getPidTimeout) : this(new PartitionConfig
        {
            GetPidTimeout = getPidTimeout,
            RebalanceRequestTimeout = identityHandoverTimeout
        })
        {
        }
        
        public PartitionIdentityLookup() : this(new PartitionConfig())
        {
        }

        public PartitionIdentityLookup(PartitionConfig? config)
        {
            _config = config ?? new PartitionConfig();
            _getPidTimeout = _config.GetPidTimeout;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken notUsed)
        {
            using var cts = new CancellationTokenSource(_getPidTimeout);
            //Get address to node owning this ID
            var (identityOwner, topologyHash) = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("[PartitionIdentity] Identity belongs to {Address}", identityOwner);
            }
            if (string.IsNullOrEmpty(identityOwner)) return null;

            var remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            var req = new ActivationRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                ClusterIdentity = clusterIdentity,
                TopologyHash = topologyHash
            };

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("[PartitionIdentity] Requesting remote PID from {Partition}:{Remote} {@Request}", identityOwner, remotePid, req);
            }

            try
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"Sending Request {req.RequestId}");

                var resp = await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, cts.Token);

                if (resp?.Pid != null) return resp.Pid;

                if (_config.DeveloperLogging)
                    Console.WriteLine("Failed");

                return null;
            }
            //TODO: decide if we throw or return null
            catch (DeadLetterException)
            {
                Logger.LogInformation("[PartitionIdentity] Remote PID request deadletter {@Request}, identity Owner {Owner}", req, identityOwner);
                return null;
            }
            catch (TimeoutException)
            {
                if (_config.DeveloperLogging)
                {
                    try
                    {
                        var resp = await _cluster.System.Root.RequestAsync<Touched?>(remotePid, new Touch(), CancellationTokens.FromSeconds(2));

                        if (resp == null)
                        {
                            if (_config.DeveloperLogging)
                                Console.WriteLine("Actor is blocked....");
                        }
                    }
                    catch
                    {
                        if (_config.DeveloperLogging)
                            Console.WriteLine("Actor is blocked....");
                    }
                }
                

                Logger.LogInformation("[PartitionIdentity] Remote PID request timeout {@Request}, identity Owner {Owner}", req, identityOwner);
                return null;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[PartitionIdentity] Error occured requesting remote PID {@Request}, identity Owner {Owner}", req, identityOwner);
                return null;
            }
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                ClusterIdentity = clusterIdentity,
            };

            _cluster.MemberList.BroadcastEvent(activationTerminated);

            return Task.CompletedTask;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            _partitionManager = new PartitionManager(cluster, isClient, _config);
            _partitionManager.Setup();
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            _partitionManager.Shutdown();
            return Task.CompletedTask;
        }

        public enum Mode
        {
            /// <summary>
            /// Each member queries every member to get the currently owned identities
            /// </summary>
            Pull,

            /// <summary>
            /// Experimental: Each activation owner publishes activations to the current identity owner
            /// </summary>
            Push
        }

        public enum Send
        {
            /// <summary>
            /// Experimental: Only identities which have changed owner since the last completed topology rebalance are sent.
            /// </summary>
            Delta,

            /// <summary>
            /// All activations are sent on every topology rebalance
            /// </summary>
            Full
        }
    }
}