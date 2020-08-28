using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    //This actor is responsible to keep track of identities owned by this member
    //it does not manage the cluster spawned actors itself, only identity->remote PID management
    //TLDR; this is a partition/bucket in the distributed hash table which makes up the identity lookup
    //
    //for spawning/activating cluster actors see PartitionActivator.cs
    internal class PartitionIdentityActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        private readonly Dictionary<string, (PID pid, string kind)> _partitionLookup =
            new Dictionary<string, (PID pid, string kind)>(); //actor/grain name to PID

        private readonly PartitionManager _partitionManager;
        private readonly Rendezvous _rdv = new Rendezvous();

        private readonly Dictionary<string, Task<ActivationResponse>> _spawns =
            new Dictionary<string, Task<ActivationResponse>>();

        private ulong _eventId;
        private DateTime _lastEventTimestamp;

        public PartitionIdentityActor(Cluster cluster, PartitionManager partitionManager)
        {
            _logger = Log.CreateLogger($"{nameof(PartitionIdentityActor)}-{cluster.LoggerId}");
            _cluster = cluster;
            _partitionManager = partitionManager;
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started _                  => Start(context),
                ReceiveTimeout _           => ReceiveTimeout(context),
                ActivationRequest msg      => GetOrSpawn(msg, context),
                ActivationTerminated msg   => ActivationTerminated(msg, context),
                ClusterTopology msg        => ClusterTopology(msg, context),
                _                          => Actor.Done
            };

        private Task Start(IContext context)
        {
            _lastEventTimestamp = DateTime.Now;
            _logger.LogDebug("Started");
            //       context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            return Actor.Done;
        }

        private Task ReceiveTimeout(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            _logger.LogInformation("I am idle");
            return Actor.Done;
        }

        private async Task ClusterTopology(ClusterTopology msg, IContext context)
        {
            if (_eventId >= msg.EventId)
            {
                return;
            }

            _eventId = msg.EventId;
            _lastEventTimestamp = DateTime.Now;
            var members = msg.Members.ToArray();

            _rdv.UpdateMembers(members);

            //remove all identities we do no longer own.
            _partitionLookup.Clear();

            _logger.LogWarning("--- Topology change --- {EventId} --- pausing interactions for 1 sec ---",
                _eventId
            );

            var requests = new List<Task<IdentityHandoverResponse>>();
            var requestMsg = new IdentityHandoverRequest
            {
                EventId = _eventId,
                Address = context.Self!.Address
            };

            requestMsg.Members.AddRange(members);

            foreach (var member in members)
            {
                var activatorPid = _partitionManager.RemotePartitionPlacementActor(member.Address);
                var request =
                    context.RequestAsync<IdentityHandoverResponse>(activatorPid, requestMsg, TimeSpan.FromSeconds(5));
                requests.Add(request);
            }

            try
            {
                _logger.LogDebug("Requesting ownerships");

                //built in timeout on each request above
                var responses = await Task.WhenAll(requests);
                _logger.LogDebug("Got ownerships {EventId}", _eventId);

                foreach (var response in responses)
                {
                    foreach (var actor in response.Actors)
                    {
                        TakeOwnership(actor);

                        if (!_partitionLookup.ContainsKey(actor.Identity))
                        {
                            _logger.LogError("Ownership bug, we should own {Identity}", actor.Identity);
                        }
                        else
                        {
                            _logger.LogDebug("I have ownership of {Identity}", actor.Identity);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                _logger.LogError("Failed to get identities");
            }


            //always do this when a member leaves, we need to redistribute the distributed-hash-table
            //no ifs or else, just always
            //ClearInvalidOwnership(context);

            var membersLookup = msg.Members.ToDictionary(m => m.Address, m => m);

            //scan through all id lookups and remove cases where the address is no longer part of cluster members
            foreach (var (actorId, (pid, _)) in _partitionLookup.ToArray())
            {
                if (!membersLookup.ContainsKey(pid.Address))
                {
                    _partitionLookup.Remove(actorId);
                }
            }
        }

        private Task ActivationTerminated(ActivationTerminated msg, IContext context)
        {
            var ownerAddress = _rdv.GetOwnerMemberByIdentity(msg.Identity);
            if (ownerAddress != context.Self.Address)
            {
                var ownerPid = _partitionManager.RemotePartitionIdentityActor(ownerAddress);
                _logger.LogWarning("Tried to terminate activation on wrong node, forwarding");
                context.Forward(ownerPid);

                return Actor.Done;
            }

            //TODO: handle correct incarnation/version
            _logger.LogDebug("Terminated {Pid}", msg.Pid);
            _partitionLookup.Remove(msg.Identity);
            return Actor.Done;
        }

        private void TakeOwnership(Activation msg)
        {
            if (_partitionLookup.TryGetValue(msg.Identity, out var existing))
            {
                //these are the same, that's good, just ignore message
                if (existing.pid.Address == msg.Pid.Address)
                {
                    return;
                }
            }

            _logger.LogDebug("Taking Ownership of: {Identity}, pid: {Pid}", msg.Identity, msg.Pid);
            _partitionLookup[msg.Identity] = (msg.Pid, msg.Kind);
        }


        private Task GetOrSpawn(ActivationRequest msg, IContext context)
        {
            var ownerAddress = _rdv.GetOwnerMemberByIdentity(msg.Identity);
            if (ownerAddress != context.Self.Address)
            {
                var ownerPid = _partitionManager.RemotePartitionIdentityActor(ownerAddress);
                _logger.LogWarning("Tried to spawn on wrong node, forwarding");
                context.Forward(ownerPid);

                return Actor.Done;
            }

            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.Identity, out var info))
            {
                context.Respond(new ActivationResponse {Pid = info.pid});
                return Actor.Done;
            }

            if (SendLater(msg, context))
            {
                return Actor.Done;
            }

            //Get activator
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activatorAddress))
            {
                //No activator currently available, return unavailable
                _logger.LogWarning("No members currently available");
                context.Respond(new ActivationResponse {Pid = null});
                return Actor.Done;
            }

            //What is this?
            //in case the actor of msg.Name is not yet spawned. there could be multiple re-entrant
            //messages requesting it, we just reuse the same task for all those
            //once spawned, the key is removed from this dict
            if (!_spawns.TryGetValue(msg.Identity, out var res))
            {
                res = SpawnRemoteActor(msg, activatorAddress);
                _spawns.Add(msg.Identity, res);
            }

            //Await SpawningProcess
            context.ReenterAfter(
                res,
                rst =>
                {
                    var response = res.Result;
                    //TODO: as this is async, there might come in multiple ActivationRequests asking for this
                    //Identity, causing multiple activations


                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfer.
                    if (_partitionLookup.TryGetValue(msg.Identity, out info))
                    {
                        context.Respond(new ActivationResponse {Pid = info.pid});
                        return Actor.Done;
                    }

                    //Check if process is faulted
                    if (rst.IsFaulted)
                    {
                        context.Respond(response);
                        return Actor.Done;
                    }


                    _partitionLookup[msg.Identity] = (response.Pid, msg.Kind);
                    context.Respond(response);
                    _spawns.Remove(msg.Identity);
                    return Actor.Done;
                }
            );
            
            return Actor.Done;
        }

        private bool SendLater(object msg, IContext context)
        {
            //TODO: buffer this in a queue and consume once we are past timestamp
            if (DateTime.Now > _lastEventTimestamp.AddSeconds(5))
            {
                return false;
            }

            var self = context.Self;
            Task.Delay(100).ContinueWith(t => { _cluster.System.Root.Send(self, msg); });
            return true;

        }


        private async Task<ActivationResponse> SpawnRemoteActor(ActivationRequest req, string activator)
        {
            try
            {
                _logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activator, req.Identity,
                    req.Kind
                );
                return await ActivateAsync(activator, req.Identity, req.Kind, _cluster.Config!.TimeoutTimespan);
            }
            catch
            {
                return null;
            }
        }

        //identical to Remote.SpawnNamedAsync, just using the special partition-activator for spawning
        private async Task<ActivationResponse> ActivateAsync(string address, string identity, string kind,
            TimeSpan timeout)
        {
            var activator = _partitionManager.RemotePartitionPlacementActor(address);

            var eventId = _eventId;
            var res = await _cluster.System.Root.RequestAsync<ActivationResponse>(
                activator, new ActivationRequest
                {
                    Kind = kind,
                    Identity = identity
                }, timeout
            );

            return res;
        }
    }
}