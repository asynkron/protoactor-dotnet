// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    //This actor is responsible to keep track of identities owned by this member
    //it does not manage the cluster spawned actors itself, only identity->remote PID management
    //TLDR; this is a partition/bucket in the distributed hash table which makes up the identity lookup
    //
    //for spawning/activating cluster actors see PartitionActivator.cs
    class PartitionIdentityActor : IActor
    {
        private readonly Cluster _cluster;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();
        private readonly string _myAddress;

        private readonly Dictionary<ClusterIdentity, PID> _partitionLookup = new(); //actor/grain name to PID

        private MemberHashRing _memberHashRing = new(ImmutableList<Member>.Empty);

        private readonly Dictionary<ClusterIdentity, (TaskCompletionSource<ActivationResponse> Response, string activationAddress)> _spawns = new();

        private ulong _topologyHash;
        private HandoverState? _currentHandover;

        private readonly TimeSpan _identityHandoverTimeout;
        private readonly PartitionConfig _config;

        private TaskCompletionSource<ulong>? _rebalanceTcs;

        private HashSet<string> _currentMemberAddresses = new();

        public PartitionIdentityActor(Cluster cluster, TimeSpan identityHandoverTimeout, PartitionConfig config)
        {
            _cluster = cluster;
            _myAddress = cluster.System.Address;
            _identityHandoverTimeout = identityHandoverTimeout;
            _config = config;
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started                  => OnStarted(context),
                ActivationRequest msg    => OnActivationRequest(msg, context),
                ActivationTerminated msg => OnActivationTerminated(msg),
                ClusterTopology msg      => OnClusterTopology(msg, context),
                IdentityHandover msg     => OnIdentityHandover(msg, context),
                _                        => Task.CompletedTask
            };

        private Task OnIdentityHandover(IdentityHandover msg, IContext context)
        {
            if (context.Sender is null)
            {
                Logger.LogWarning("IdentityHandover received with null sender");
                return Task.CompletedTask;
            }

            context.Respond(new IdentityHandoverAck
                {
                    ChunkId = msg.ChunkId,
                    TopologyHash = msg.TopologyHash
                }
            );

            if (msg.TopologyHash != _topologyHash)
            {
                Logger.LogWarning("IdentityHandover with non-matching topology hash {MessageTopologyHash} instead of {CurrentTopologyHash}",
                    msg.TopologyHash, _topologyHash
                );
                return Task.CompletedTask;
            }

            if (_currentHandover is null)
            {
                Logger.LogWarning("IdentityHandover received when member is not re-balancing");
                return Task.CompletedTask;
            }

            foreach (var activation in msg.Actors)
            {
                TakeOverIdentity(activation.ClusterIdentity, activation.Pid);
            }

            if (_currentHandover.IsFinalHandoverMessage(context.Sender, msg))
            {
                if (_config.DeveloperLogging)
                {
                    Console.WriteLine("Completed rebalance from all members for topology " + _topologyHash);
                }

                _currentHandover = null;
                _rebalanceTcs?.TrySetResult(_topologyHash);
                _rebalanceTcs = null;
            }

            return Task.CompletedTask;
        }

        private Task OnStarted(IContext context)
        {
            var self = context.Self;
            _cluster.System.EventStream.Subscribe<ActivationTerminated>(e => _cluster.System.Root.Send(self, e));

            return Task.CompletedTask;
        }

        private Task OnClusterTopology(ClusterTopology msg, IContext context)
        {
            if (_topologyHash.Equals(msg.TopologyHash))
            {
                return Task.CompletedTask;
            }

            FailSpawnsTargetingLeftMembers(msg);

            SetTopology(msg);

            if (msg.Members.Count == 0)
            {
                Logger.LogWarning("No active members in cluster topology update");
                _partitionLookup.Clear();
                return Task.CompletedTask;
            }

            SetReadyToRebalanceIfNoMoreWaitingSpawns();
            DiscardInvalidatedActivations();

            _rebalanceTcs ??= new TaskCompletionSource<ulong>();

            if (_config.Mode == PartitionIdentityLookup.Mode.Push)
            {
                _currentHandover = new HandoverState(msg);
                return Task.CompletedTask;
            }

            Logger.LogInformation("{SystemId} Starting to wait for activations to complete:, {CurrentTopology}", _cluster.System.Id, _topologyHash);

            var timer = Stopwatch.StartNew();

            var topologyValidityToken = msg.TopologyValidityToken!.Value;
            var waitUntilInFlightActivationsAreCompleted =
                _cluster.Gossip.WaitUntilInFlightActivationsAreCompleted(_identityHandoverTimeout, topologyValidityToken);

            context.ReenterAfter(waitUntilInFlightActivationsAreCompleted, consensusResult => {
                    if (_topologyHash != msg.TopologyHash || topologyValidityToken.IsCancellationRequested)
                    {
                        // Cancelled
                        return Task.CompletedTask;
                    }

                    timer.Stop();
                    var allNodesCompletedActivations = consensusResult.Result.consensus;

                    if (allNodesCompletedActivations)
                    {
                        Logger.LogDebug("{SystemId} All nodes OK, Initiating rebalance:, {CurrentTopology} {ConsensusHash} after {Duration}",
                            _cluster.System.Id, _topologyHash, consensusResult.Result.topologyHash, timer.Elapsed
                        );
                    }
                    else
                    {
                        Logger.LogError(
                            "{SystemId} Consensus not reached, Initiating rebalance:, {CurrentTopology} {ConsensusHash} after {Duration}",
                            _cluster.System.Id, _topologyHash, consensusResult.Result.topologyHash, timer.Elapsed
                        );
                    }

                    InitRebalance(msg, context, topologyValidityToken);
                    return Task.CompletedTask;
                }
            );
            return Task.CompletedTask;
        }

        private void DiscardInvalidatedActivations()
        {
            var members = _currentMemberAddresses;
            var invalid = _partitionLookup
                .Where(kv => !members.Contains(kv.Value.Address) ||
                             !_memberHashRing.GetOwnerMemberByIdentity(kv.Key).Equals(_myAddress, StringComparison.InvariantCultureIgnoreCase)
                )
                .Select(kv => kv.Key)
                .ToList();

            foreach (var clusterIdentity in invalid)
            {
                _partitionLookup.Remove(clusterIdentity);
            }
        }

        private void SetTopology(ClusterTopology msg)
        {
            _topologyHash = msg.TopologyHash;
            _memberHashRing = new MemberHashRing(msg.Members);
            _currentMemberAddresses = msg.Members.Select(it => it.Address).ToHashSet();
        }

        private void FailSpawnsTargetingLeftMembers(ClusterTopology topology)
        {
            if (topology.Left.Count == 0) return;

            var leftAddresses = topology.Left.Select(member => member.Address).ToHashSet();

            var spawningOnLeftMembers = _spawns.Where(it => leftAddresses.Contains(it.Value.activationAddress)).ToList();
            if (spawningOnLeftMembers.Count == 0) return;

            var result = new ActivationResponse
            {
                Failed = true
            };

            foreach (var (clusterIdentity, invalidSpawn) in spawningOnLeftMembers)
            {
                invalidSpawn.Response.TrySetResult(result);
                _spawns.Remove(clusterIdentity);
            }

            Logger.LogDebug("Removed {Count} spawns targeting previous members", spawningOnLeftMembers.Count);
        }

        private void SetReadyToRebalanceIfNoMoreWaitingSpawns()
        {
            if (_spawns.Count == 0)
            {
                _cluster.Gossip.SetInFlightActivationsCompleted(_topologyHash);
            }
        }

        private void InitRebalance(ClusterTopology msg, IContext context, CancellationToken cancellationToken)
        {
            var workerPid = SpawnRebalanceWorker(context, cancellationToken);
            var rebalanceTask = context.RequestAsync<PartitionsRebalanced>(workerPid, new IdentityHandoverRequest
                {
                    TopologyHash = _topologyHash,
                    Address = _myAddress,
                    Members = {msg.Members}
                }, cancellationToken
            );

            context.ReenterAfter(rebalanceTask, task => {
                    if (task.IsCompletedSuccessfully)
                    {
                        return OnPartitionsRebalanced(task.Result, context);
                    }

                    Logger.LogError("Partition Rebalance cancelled for {TopologyHash}", _topologyHash);
                    return Task.CompletedTask;
                }
            );
        }

        private PID SpawnRebalanceWorker(IContext context, CancellationToken cancellationToken) => context.Spawn(
            Props.FromProducer(() => new PartitionIdentityRebalanceWorker(_config.RebalanceRequestTimeout, cancellationToken))
        );

        private Task OnPartitionsRebalanced(PartitionsRebalanced msg, IContext context)
        {
            if (msg.TopologyHash != _topologyHash)
            {
                if (_config.DeveloperLogging)
                {
                    Console.WriteLine($"Rebalance with outdated TopologyHash {msg.TopologyHash}!={_topologyHash}");
                }

                Logger.LogWarning("Rebalance with outdated TopologyHash {Received} instead of {Current}", msg.TopologyHash, _topologyHash);
                return Task.CompletedTask;
            }

            _cluster.Gossip.SetRebalanceCompleted(_topologyHash);

            if (_config.DeveloperLogging)
            {
                Console.WriteLine($"{context.System.Id}: Got ownerships {msg.TopologyHash} / {_topologyHash}");
            }

            //remove all identities we do no longer own.
            _partitionLookup.Clear();

            Logger.LogInformation("{SystemId} Got  ownerships of {Count} activations, for topology {TopologyHash}, ", context.System.Id,
                msg.OwnedActivations.Count, _topologyHash
            );

            foreach (var (clusterIdentity, activation) in msg.OwnedActivations)
            {
                TakeOverIdentity(clusterIdentity, activation);
            }

            _rebalanceTcs?.TrySetResult(_topologyHash);
            _rebalanceTcs = null;

            return Task.CompletedTask;
        }

        private void TakeOverIdentity(ClusterIdentity clusterIdentity, PID activation)
        {
            if (_partitionLookup.TryAdd(clusterIdentity, activation)) return;

            var existingActivation = _partitionLookup[clusterIdentity];

            if (existingActivation.Equals(activation))
            {
                Logger.LogDebug("Got the same activations twice {ClusterIdentity}: {Activation}", clusterIdentity, existingActivation);
            }
            else
            {
                // Already present, duplicate?
                Logger.LogError("Got duplicate activations of {ClusterIdentity}: {Activation1}, {Activation2}",
                    clusterIdentity,
                    existingActivation,
                    activation
                );
            }
        }

        private Task OnActivationTerminated(ActivationTerminated msg)
        {
            if (_spawns.ContainsKey(msg.ClusterIdentity))
            {
                return Task.CompletedTask;
            }

            //we get this via broadcast to all nodes, remove if we have it, or ignore
            Logger.LogDebug("[PartitionIdentityActor] Terminated {Pid}", msg.Pid);

            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var existingActivation) && existingActivation.Equals(msg.Pid))
            {
                _partitionLookup.Remove(msg.ClusterIdentity);
            }

            _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, msg.Pid);

            return Task.CompletedTask;
        }

        private Task OnActivationRequest(ActivationRequest msg, IContext context)
        {
            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var pid))
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"Found existing activation for {msg.RequestId} {msg.ClusterIdentity}");

                context.Respond(new ActivationResponse {Pid = pid});
                return Task.CompletedTask;
            }

            // Wait for rebalance in progress
            if (_rebalanceTcs is not null)
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"Rebalance in progress,  {msg.RequestId}");
                context.ReenterAfter(_rebalanceTcs.Task, _ => OnActivationRequest(msg, context));
                return Task.CompletedTask;
            }

            if (_memberHashRing.Count == 0)
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"No active members, {msg.RequestId}");
                RespondWithFailure(context);
                return Task.CompletedTask;
            }

            if (_config.DeveloperLogging)
                Console.WriteLine($"Got ActivationRequest {msg.RequestId}");

            if (msg.TopologyHash != _topologyHash)
            {
                var ownerAddress = _memberHashRing.GetOwnerMemberByIdentity(msg.Identity);

                if (ownerAddress != _myAddress)
                {
                    if (_config.DeveloperLogging)
                        Console.WriteLine($"Forwarding ActivationRequest {msg.RequestId} to {ownerAddress}");

                    var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
                    Logger.LogWarning("Tried to spawn on wrong node, forwarding");
                    context.Forward(ownerPid);

                    return Task.CompletedTask;
                }
            }

            //only activate members when we are all in sync
            // var c = await _cluster.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));
            //
            // if (!c)
            // {
            //     Console.WriteLine("No consensus " + _cluster.System.Id);
            // }

            //Get activator
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

            if (string.IsNullOrEmpty(activatorAddress))
            {
                if (_config.DeveloperLogging)
                    Console.Write("?");
                //No activator currently available, return unavailable
                Logger.LogWarning("No members currently available for kind {Kind}", msg.Kind);
                context.Respond(new ActivationResponse
                    {
                        Failed = true
                    }
                );
                return Task.CompletedTask;
            }

            //What is this?
            //in case the actor of msg.Name is not yet spawned. there could be multiple re-entrant
            //messages requesting it, we just reuse the same task for all those
            //once spawned, the key is removed from this dict
            if (_spawns.TryGetValue(msg.ClusterIdentity, out var res))
            {
                // Just waits for the already in-progress activation to complete (or fail)
                context.ReenterAfter(res.Response.Task, task => {
                        context.Respond(task.Result);
                        return Task.CompletedTask;
                    }
                );
                return Task.CompletedTask;
            }

            // Not in progress, spawn actor

            var spawnResponse = SpawnRemoteActor(msg, activatorAddress);
            var setResponse = new TaskCompletionSource<ActivationResponse>();
            _spawns.Add(msg.ClusterIdentity, (setResponse, activatorAddress));

            //execution ends here. context.ReenterAfter is invoked once the task completes
            //but still within the actors sequential execution
            //but other messages could have been processed in between

            if (_config.DeveloperLogging)
                Console.Write("S"); //spawned
            //Await SpawningProcess
            context.ReenterAfter(
                spawnResponse,
                rst => {
                    try
                    {
                        if (rst.IsCompletedSuccessfully)
                        {
                            var response = rst.Result;

                            if (_config.DeveloperLogging)
                                Console.Write("R"); //reentered

                            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out pid))
                            {
                                if (_config.DeveloperLogging)
                                    Console.Write("C"); //cached

                                if (response.Pid is not null && !response.Pid.Equals(pid))
                                {
                                    context.Stop(response.Pid); // Stop duplicate activation
                                }

                                Respond(new ActivationResponse {Pid = pid, TopologyHash = _topologyHash});
                                return Task.CompletedTask;
                            }

                            if (response?.Pid != null)
                            {
                                if (_config.DeveloperLogging)
                                    Console.Write("A"); //activated

                                if (response.TopologyHash != _topologyHash) // Topology changed between request and response
                                {
                                    if (!_currentMemberAddresses.Contains(response.Pid.Address))
                                    {
                                        // No longer part of cluster, dropped
                                        Logger.LogWarning("Received activation response {@Response}, no longer part of cluster", response);
                                        Respond(new ActivationResponse {Failed = true});
                                        return Task.CompletedTask;
                                    }

                                    var currentActivatorAddress = _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

                                    if (_myAddress != currentActivatorAddress)
                                    {
                                        //TODO: Stop it or handover?
                                        Logger.LogWarning("Misplaced spawn: {ClusterIdentity}, {Pid}", msg.ClusterIdentity, response.Pid);
                                    }
                                }

                                _partitionLookup[msg.ClusterIdentity] = response.Pid;
                                Respond(response);

                                return Task.CompletedTask;
                            }
                        }
                        else
                        {
                            Logger.LogError(rst.Exception, "Spawn task failed");
                        }
                    }
                    catch (Exception x)
                    {
                        Logger.LogError(x, "Spawning failed");
                    }
                    finally
                    {
                        var wasPresent = _spawns.Remove(msg.ClusterIdentity);

                        if (wasPresent && _rebalanceTcs is not null && _spawns.Count == 0)
                        {
                            SetReadyToRebalanceIfNoMoreWaitingSpawns();
                        }
                    }

                    if (_config.DeveloperLogging)
                        Console.Write("F"); //failed
                    Respond(new ActivationResponse {Failed = true});

                    return Task.CompletedTask;

                    // The response both responds to the initial activator, but also any other waiting reentrant requests
                    void Respond(ActivationResponse response)
                    {
                        context.Respond(response);
                        setResponse.TrySetResult(response);
                    }
                }
            );
            return Task.CompletedTask;
        }

        private void ActivateAfterConsensus(ActivationRequest msg, IContext context)
            => context.ReenterAfter(_cluster.MemberList.TopologyConsensus(CancellationToken.None), _ => OnActivationRequest(msg, context));

        // private void HandleMisplacedIdentity(ActivationRequest msg, ActivationResponse response, string? activatorAddress, IContext context)
        // {
        //     _spawns.Remove(msg.ClusterIdentity);
        //     if (activatorAddress is null)
        //     {
        //         context.Stop(response.Pid); // We could possibly move the activation to the new owner?
        //         RespondWithFailure(context);
        //     }
        //     else
        //     {
        //         var pid = PartitionManager.RemotePartitionIdentityActor(activatorAddress);
        //         context.RequestReenter<ActivationResponse>(pid, new ActivationHandover
        //         {
        //             ClusterIdentity = msg.ClusterIdentity,
        //             RequestId = msg.RequestId,
        //             TopologyHash = msg.TopologyHash,
        //             Pid = response.Pid
        //         }, responseTask => {
        //             if (responseTask.IsCompletedSuccessfully)
        //             {
        //                 context.Respond(responseTask.Result);
        //             }
        //             else
        //             {
        //                 context.Stop(response.Pid);
        //                 RespondWithFailure(context);
        //             }
        //             
        //             
        //             return Task.CompletedTask;
        //         }, CancellationTokens.WithTimeout(_identityHandoverTimeout));
        //     }
        // }

        private static void RespondWithFailure(IContext context) => context.Respond(new ActivationResponse {Failed = true});

        private async Task<ActivationResponse> SpawnRemoteActor(ActivationRequest req, string activatorAddress)
        {
            try
            {
                Logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activatorAddress, req.Identity,
                    req.Kind
                );
                var timeout = _cluster.Config.TimeoutTimespan;
                var activatorPid = PartitionManager.RemotePartitionPlacementActor(activatorAddress);

                var res = await _cluster.System.Root.RequestAsync<ActivationResponse>(activatorPid, req, timeout);
                return res;
            }
            catch
            {
                return new ActivationResponse
                {
                    Failed = true
                };
            }
        }
    }

    record PartitionsRebalanced(Dictionary<ClusterIdentity, PID> OwnedActivations, ulong TopologyHash);
}