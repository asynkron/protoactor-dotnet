// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    internal class PartitionPlacementActor : IActor
    {
        private readonly ILogger _logger;
        private readonly Cluster _cluster;
        private readonly ActorSystem _system;
        private readonly Remote.Remote _remote;
        private readonly IRootContext _context;
        private readonly PartitionManager _partitionManager;
        private readonly Dictionary<string,(PID pid,string kind,string identityOwner)> _myActors = new Dictionary<string, (PID pid,string kind,string identityOwner)>();
        
        public PartitionPlacementActor(Cluster cluster, PartitionManager partitionManager)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
            _context = _system.Root;
            _partitionManager = partitionManager;
            _logger = Log.CreateLogger($"{nameof(PartitionPlacementActor)}-{cluster.Id}");
        }
        
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case MemberLeftEvent _:
                case MemberJoinedEvent _:
                    //TODO: check what needs to be transferred
                    HandleOwnershipTransfer(context);
                    
                    break;
                case ActorPidRequest msg:
                    HandleActorPidRequest(context, msg);
                    break;
            }
            return Actor.Done;
        }

        private void HandleOwnershipTransfer(IContext context)
        {
            foreach (var (identity, (pid,kind,oldOwnerAddress)) in _myActors)
            {
                var newOwnerAddress = _partitionManager.Selector.GetIdentityOwner(identity);
                if (newOwnerAddress != oldOwnerAddress)
                {
                    _logger.LogError($"TRANSFER {pid} FROM {oldOwnerAddress} TO {newOwnerAddress}");
                    var owner = _partitionManager.RemotePartitionIdentityActor(newOwnerAddress);
                    context.Send(owner, new TakeOwnership {Name = identity,Kind = kind, Pid = pid});
                }
            }
        }

        private void HandleActorPidRequest(IContext context, ActorPidRequest msg)
        {
            var props = _remote.GetKnownKind(msg.Kind);
            var name = msg.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = _system.ProcessRegistry.NextId();
            }

            try
            {
                //spawn and remember this actor
                var pid = _context.SpawnNamed(props, name);
                _myActors[name] = (pid, msg.Kind, context.Sender.Address);

                var response = new ActorPidResponse {Pid = pid};
                context.Respond(response);
            }
            catch (ProcessNameExistException ex)
            {
                var response = new ActorPidResponse
                {
                    Pid = ex.Pid,
                    StatusCode = (int) ResponseStatusCode.ProcessNameAlreadyExist
                };
                context.Respond(response);
            }
            catch
            {
                var response = new ActorPidResponse
                {
                    StatusCode = (int) ResponseStatusCode.Error
                };
                context.Respond(response);

                throw;
            }
        }
    }
}