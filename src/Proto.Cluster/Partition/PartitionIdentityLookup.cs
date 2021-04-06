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
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityLookup>();
        private PartitionManager _partitionManager = null!;
        private readonly TimeSpan _identityHandoverTimeout;
        private readonly TimeSpan _getPidTimeout;

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
            var identityOwner = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);
            Logger.LogDebug("Identity belongs to {address}", identityOwner);
            if (string.IsNullOrEmpty(identityOwner)) return null;

            var remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            var req = new ActivationRequest
            {
                ClusterIdentity = clusterIdentity
            };

            Logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", identityOwner, remotePid, req
            );

            try
            {
                var resp = await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

                return resp.Pid;
            }
            //TODO: decide if we throw or return null
            catch (DeadLetterException)
            {
                Logger.LogInformation("Remote PID request deadletter {@Request}, identity Owner {Owner}", req,identityOwner);
                return null;
            }
            catch (TimeoutException)
            {
                Logger.LogInformation("Remote PID request timeout {@Request}, identity Owner {Owner}", req,identityOwner);
                return null;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occured requesting remote PID {@Request}, identity Owner {Owner}", req,identityOwner);
                return null;
            }
        }


        public void DumpState(ClusterIdentity clusterIdentity)
        {
            Console.WriteLine("Memberlist members:");
            _cluster.MemberList.DumpState();

            Console.WriteLine("Partition manager selector:");
            _partitionManager.Selector.DumpState();

            //Get address to node owning this ID
            var identityOwner = _partitionManager.Selector.GetIdentityOwner(clusterIdentity.Identity);

            Console.WriteLine("Identity owner for ID:");
            Console.WriteLine(identityOwner);

            var remotePid = PartitionManager.RemotePartitionIdentityActor(identityOwner);

            var req = new ActivationRequest
            {
                ClusterIdentity = clusterIdentity
            };

            var resp = _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, CancellationTokens.WithTimeout(5000)).Result;

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