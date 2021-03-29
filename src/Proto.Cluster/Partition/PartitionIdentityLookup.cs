// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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
        private ILogger _logger = null!;
        private PartitionManager _partitionManager = null!;
        private readonly TimeSpan _identityHandoverTimeout;

        public PartitionIdentityLookup() : this(TimeSpan.FromSeconds(3))
        {
        }
        
        public PartitionIdentityLookup(TimeSpan identityHandoverTimeout)
        {
            _identityHandoverTimeout = identityHandoverTimeout;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            ct = CancellationTokens.WithTimeout(1000);
            //Get address to node owning this ID
            var identityOwner = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);
            _logger.LogDebug("Identity belongs to {address}", identityOwner);
            if (string.IsNullOrEmpty(identityOwner)) return null;

            var remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            var req = new ActivationRequest
            {
                ClusterIdentity = clusterIdentity
            };

            _logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", identityOwner, remotePid, req
            );

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req,
                        _cluster.Config!.TimeoutTimespan
                    )
                    : await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

                return resp.Pid;
            }
            //TODO: decide if we throw or return null
            catch (DeadLetterException)
            {
                return null;
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("Remote PID request timeout {@Request}", req);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured requesting remote PID {@Request}", req);
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
            _partitionManager = new PartitionManager(cluster, isClient, _identityHandoverTimeout);
            _logger = Log.CreateLogger(nameof(PartitionIdentityLookup) + "-" + _cluster.LoggerId);
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