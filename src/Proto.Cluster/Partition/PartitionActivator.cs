// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    internal class PartitionActivator : IActor
    {
        private readonly Cluster _cluster;
        private readonly ActorSystem _system;
        private readonly Remote.Remote _remote;
        private readonly IRootContext _context;
        private readonly PartitionManager _partitionManager;
        
        public PartitionActivator(Cluster cluster, PartitionManager partitionManager)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
            _context = _system.Root;
            _partitionManager = partitionManager;
        }
        
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ActorPidRequest msg:
                    var props = _remote.GetKnownKind(msg.Kind);
                    var name = msg.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = _system.ProcessRegistry.NextId();
                    }

                    try
                    {
                        var pid = _context.SpawnNamed(props, name);
                        var response = new ActorPidResponse { Pid = pid };
                        context.Respond(response);
                    }
                    catch (ProcessNameExistException ex)
                    {
                        var response = new ActorPidResponse
                        {
                            Pid = ex.Pid,
                            StatusCode = (int)ResponseStatusCode.ProcessNameAlreadyExist
                        };
                        context.Respond(response);
                    }
                    catch
                    {
                        var response = new ActorPidResponse
                        {
                            StatusCode = (int)ResponseStatusCode.Error
                        };
                        context.Respond(response);

                        throw;
                    }
                    break;
            }
            return Actor.Done;
        }
    }
}