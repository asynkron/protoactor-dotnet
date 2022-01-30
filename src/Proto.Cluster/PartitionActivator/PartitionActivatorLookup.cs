// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;

namespace Proto.Cluster.PartitionActivator
{
    public class PartitionActivatorLookup : IIdentityLookup
    {
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityLookup>();
        private readonly TimeSpan _getPidTimeout;
        private Cluster _cluster = null!;
        private PartitionActivatorManager _partitionManager = null!;

        public PartitionActivatorLookup() : this(TimeSpan.FromSeconds(1))
        {
        }

        public PartitionActivatorLookup(TimeSpan getPidTimeout)
        {
            _getPidTimeout = getPidTimeout;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken notUsed)
        {
            using var cts = new CancellationTokenSource(_getPidTimeout);
            //Get address to node owning this ID
            var owner = _partitionManager.Selector.GetOwner(clusterIdentity);
            Logger.LogDebug("Identity belongs to {Address}", owner);
            if (string.IsNullOrEmpty(owner)) return null;

            var remotePid = PartitionActivatorManager.RemotePartitionActivatorActor(owner);

            var req = new ActivationRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                ClusterIdentity = clusterIdentity
            };

            Logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", owner, remotePid, req
            );

            try
            {

                var resp = await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, cts.Token);

                return resp?.Pid;
            }
            //TODO: decide if we throw or return null
            catch (DeadLetterException)
            {
                Logger.LogInformation("Remote PID request deadletter {@Request}, identity Owner {Owner}", req, owner);
                return null;
            }
            catch (TimeoutException)
            {
                Logger.LogInformation("Remote PID request timeout {@Request}, identity Owner {Owner}", req, owner);
                return null;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occured requesting remote PID {@Request}, identity Owner {Owner}", req, owner);
                return null;
            }
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                ClusterIdentity = clusterIdentity
            };

            _cluster.MemberList.BroadcastEvent(activationTerminated);

            return Task.CompletedTask;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            _partitionManager = new PartitionActivatorManager(cluster, isClient);
            _partitionManager.Setup();
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            _partitionManager.Shutdown();
            return Task.CompletedTask;
        }
    }
}