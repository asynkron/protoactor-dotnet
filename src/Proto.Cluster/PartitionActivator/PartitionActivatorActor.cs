// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PartitionActivator
{
    public class PartitionActivatorActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<PartitionActivatorActor>();

        private readonly ShouldThrottle _wrongPartitionLogThrottle = Throttle.Create(1, TimeSpan.FromSeconds(1), wrongNodeCount => {
                if (wrongNodeCount > 1)
                {
                    Logger.LogWarning("Forwarded {SpawnCount} attempts to spawn on wrong node", wrongNodeCount);
                }
            }
        );
        private ulong _topologyHash;
        private readonly Cluster _cluster;
        private readonly PartitionActivatorManager _manager;
        private readonly Dictionary<ClusterIdentity, PID> _actors = new();
        private readonly string _myAddress;

        public PartitionActivatorActor(Cluster cluster, PartitionActivatorManager manager)
        {
            _cluster = cluster;
            _manager = manager;
            _myAddress = cluster.System.Address;
        }
        
        private Task OnStarted(IContext context)
        {
            var self = context.Self;
            _cluster.System.EventStream.Subscribe<ActivationTerminated>(e => {
                
                _cluster.System.Root.Send(self,e);
            });
            
            return Task.CompletedTask;
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started                  => OnStarted(context),
                ActivationRequest msg    => OnActivationRequest(msg, context),
                ActivationTerminated msg => OnActivationTerminated(msg, context),
                ClusterTopology msg      => OnClusterTopology(msg, context),
                _                        => Task.CompletedTask
            };

        private async Task OnClusterTopology(ClusterTopology msg, IContext context)
        {
            if (msg.TopologyHash == _topologyHash)
                return;
            
            _topologyHash = msg.TopologyHash;
            
            var toRemove = _actors
                .Where(kvp => _manager.Selector.GetOwner(kvp.Key) != _cluster.System.Id)
                .Select(kvp => kvp.Key)
                .ToList();

            //stop and remove all actors we don't own anymore
            var stopping = new List<Task>();
            foreach (var ci in toRemove)
            {
                var pid = _actors[ci];
                var stoppingTask = context.PoisonAsync(pid);
                stopping.Add(stoppingTask);
                _actors.Remove(ci);
            }

            //await graceful shutdown of all actors we no longer own
            await Task.WhenAll(stopping);
        }
        
        private Task OnActivationTerminated(ActivationTerminated msg, IContext context)
        {
            if (!_actors.ContainsKey(msg.ClusterIdentity))
            {
                return Task.CompletedTask;
            }
            //we get this via broadcast to all nodes, remove if we have it, or ignore
            Logger.LogTrace("[PartitionIdentityActor] Terminated {Pid}", msg.Pid);
            _actors.Remove(msg.ClusterIdentity);

            return Task.CompletedTask;
        }

        private Task OnActivationRequest(ActivationRequest msg, IContext context)
        {
         
            //who owns this?
            var ownerAddress = _manager.Selector.GetOwner(msg.ClusterIdentity);

            //is it not me?
            if (ownerAddress != _myAddress)
            {
                //get the owner
                var ownerPid = PartitionActivatorManager.RemotePartitionActivatorActor(ownerAddress);

                if (_wrongPartitionLogThrottle().IsOpen())
                {
                    Logger.LogWarning("Tried to spawn on wrong node, forwarding");
                }
                context.Forward(ownerPid);

                return Task.CompletedTask;
            }
            
            

            if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
            {
                context.Respond(new ActivationResponse
                {
                    Pid = existing,
                });
            }
            else
            {
                var kind = _cluster.GetClusterKind(msg.Kind);
                var pid = context.Spawn(kind.Props);
                _actors.Add(msg.ClusterIdentity, pid);
                context.Respond(new ActivationResponse
                    {
                        Pid = pid,
                    }
                );
            }

            return Task.CompletedTask;
        }
    }
}