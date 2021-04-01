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
        private readonly TimeSpan _getPidTimeout;
        private readonly TimeSpan _identityHandoverTimeout;
        private Cluster _cluster = null!;
        private ILogger _logger = null!;
        private PartitionManager _partitionManager = null!;

        public PartitionIdentityLookup() : this(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))
        {
        }

        public PartitionIdentityLookup(TimeSpan identityHandoverTimeout, TimeSpan getPidTimeout)
        {
            _identityHandoverTimeout = identityHandoverTimeout;
            _getPidTimeout = getPidTimeout;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            ct = CancellationTokens.WithTimeout(_getPidTimeout);
            //Get address to node owning this ID
            string? identityOwner = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);
            _logger.LogDebug("Identity belongs to {address}", identityOwner);
            if (string.IsNullOrEmpty(identityOwner))
            {
                return null;
            }

            PID? remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            ActivationRequest? req = new ActivationRequest {ClusterIdentity = clusterIdentity};

            _logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", identityOwner, remotePid, req
            );

            try
            {
                ActivationResponse? resp =
                    await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

                return resp.Pid;
            }
            //TODO: decide if we throw or return null
            catch (DeadLetterException)
            {
                _logger.LogInformation("Remote PID request deadletter {@Request}, identity Owner {Owner}", req,
                    identityOwner);
                return null;
            }
            catch (TimeoutException)
            {
                _logger.LogInformation("Remote PID request timeout {@Request}, identity Owner {Owner}", req,
                    identityOwner);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured requesting remote PID {@Request}, identity Owner {Owner}", req,
                    identityOwner);
                return null;
            }
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            ActivationTerminated? activationTerminated = new ActivationTerminated
            {
                Pid = pid, ClusterIdentity = clusterIdentity
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


        public void DumpState(ClusterIdentity clusterIdentity)
        {
            Console.WriteLine("Memberlist members:");
            _cluster.MemberList.DumpState();

            Console.WriteLine("Partition manager selector:");
            _partitionManager.Selector.DumpState();

            //Get address to node owning this ID
            string? identityOwner = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);

            Console.WriteLine("Identity owner for ID:");
            Console.WriteLine(identityOwner);

            PID? remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            ActivationRequest? req = new ActivationRequest {ClusterIdentity = clusterIdentity};

            ActivationResponse? resp = _cluster.System.Root
                .RequestAsync<ActivationResponse>(remotePid, req, CancellationTokens.WithTimeout(5000)).Result;

            Console.WriteLine("Target Pid:");

            if (resp == null)
            {
                Console.WriteLine("Null response");
            }
            else if (resp.Pid == null)
            {
                Console.WriteLine("Null PID");
            }
            else
            {
                Console.WriteLine(resp.Pid);
            }
        }
    }
}
