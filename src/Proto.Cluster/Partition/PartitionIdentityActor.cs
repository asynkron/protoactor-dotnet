// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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

namespace Proto.Cluster.Partition;

//This actor is responsible to keep track of identities owned by this member
//it does not manage the cluster spawned actors itself, only identity->remote PID management
//TLDR; this is a partition/bucket in the distributed hash table which makes up the identity lookup
//
//for spawning/activating cluster actors see PartitionActivator.cs
internal class PartitionIdentityActor : IActor
{
    private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();

    private readonly Cluster _cluster;
    private readonly PartitionConfig _config;
    private readonly MemberStatistics _memberStats = new();
    private readonly string _myAddress;

    private readonly Dictionary<ClusterIdentity, PID> _partitionLookup = new(); // actor/grain name to PID

    private readonly
        Dictionary<ClusterIdentity, (TaskCompletionSource<ActivationResponse> Response, string activationAddress)>
        _spawns = new();

    private HandoverSink? _currentHandover;
    private HashSet<string> _currentMemberAddresses = new();
    private ClusterTopology? _currentTopology;

    private ClusterTopology? _deltaTopology;
    private MemberHashRing _memberHashRing = new(ImmutableList<Member>.Empty);
    private TaskCompletionSource<ulong>? _rebalanceTcs;
    private Stopwatch? _rebalanceTimer;

    public PartitionIdentityActor(Cluster cluster, PartitionConfig config)
    {
        _cluster = cluster;
        _myAddress = cluster.System.Address;
        _config = config;
    }

    private ulong TopologyHash => _currentTopology?.TopologyHash ?? 0;

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                  => OnStarted(context),
            ActivationRequest msg    => OnActivationRequest(msg, context),
            ActivationTerminated msg => OnActivationTerminated(msg),
            ClusterTopology msg      => OnClusterTopology(msg, context),
            IdentityHandover msg     => OnIdentityHandover(msg, context),
            PartitionCompleted msg   => OnPartitionCompleted(msg, context),
            _                        => Task.CompletedTask
        };

    /// <summary>
    ///     Used by pull mode, the partition identity actor will spawn workers to rebalance against each member.
    ///     They will send a message back upon completion of each partition, containing all Identity handover messages from
    ///     that member.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    private Task OnPartitionCompleted(PartitionCompleted msg, IContext context)
    {
        if (_currentHandover is null)
        {
            Logger.LogWarning("[PartitionIdentity] PartitionCompleted received when member is not re-balancing");

            return Task.CompletedTask;
        }

        foreach (var handover in msg.Chunks)
        {
            ReceiveIdentityHandover(_currentHandover, handover, msg.MemberAddress, context);
        }

        return Task.CompletedTask;
    }

    private Task OnIdentityHandover(IdentityHandover msg, IContext context)
    {
        if (_config.Mode != PartitionIdentityLookup.Mode.Push)
        {
            Logger.LogError(
                "[PartitionIdentity] IdentityHandover push from {Address} received in pull mode. All members need to use the same partition rebalance algorithm",
                context.Sender?.Address
            );

            return Task.CompletedTask;
        }

        if (context.Sender is null)
        {
            Logger.LogError("[PartitionIdentity] IdentityHandover received with null sender");

            return Task.CompletedTask;
        }

        if (msg.TopologyHash != TopologyHash)
        {
            Logger.LogWarning(
                "[PartitionIdentity] IdentityHandover with non-matching topology hash {MessageTopologyHash} instead of {CurrentTopologyHash}",
                msg.TopologyHash, TopologyHash
            );

            Acknowledge(IdentityHandoverAck.Types.State.IncorrectTopology);

            return Task.CompletedTask;
        }

        Acknowledge(IdentityHandoverAck.Types.State.Processed);

        if (_currentHandover is null)
        {
            Logger.LogWarning("[PartitionIdentity] IdentityHandover received when member is not re-balancing");

            return Task.CompletedTask;
        }

        var address = context.Sender.Address;

        ReceiveIdentityHandover(_currentHandover, msg, address, context);

        return Task.CompletedTask;

        void Acknowledge(IdentityHandoverAck.Types.State state) =>
            context.Respond(new IdentityHandoverAck
                {
                    ChunkId = msg.ChunkId,
                    TopologyHash = msg.TopologyHash,
                    ProcessingState = state
                }
            );
    }

    private void ReceiveIdentityHandover(HandoverSink sink, IdentityHandover msg, string address, IContext context)
    {
        if (!sink.Receive(address, msg))
        {
            return; // Not the final message in the topology update
        }

        if (_config.Send == PartitionIdentityLookup.Send.Delta)
        {
            if (!ValidateOrRetryDeltaHandover(sink, address, context))
            {
                return;
            }
        }

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "[PartitionIdentity] Topology {TopologyHash} rebalance completed in {Elapsed}, received {@Stats}",
                TopologyHash, _rebalanceTimer?.Elapsed, sink.CompletedHandovers);
        }

        _rebalanceTimer = null;
        _cluster.Gossip.SetRebalanceCompleted(TopologyHash);

        if (_config.Mode == PartitionIdentityLookup.Mode.Pull && _config.Send == PartitionIdentityLookup.Send.Delta)
        {
            // Establish current rebalanced topology as a baseline for next delta handover.
            _deltaTopology = _currentTopology;
        }

        _currentHandover = null;
        _rebalanceTcs?.TrySetResult(TopologyHash);
        _rebalanceTcs = null;
    }

    private bool ValidateOrRetryDeltaHandover(HandoverSink sink, string address, IContext context)
    {
        var incomplete = GetIncompletePartitionAddresses(sink, address);

        if (incomplete.Count == 0)
        {
            return true;
        }

        DiscardActivationsByMemberAddresses(incomplete);

        foreach (var memberAddress in incomplete)
        {
            sink.ResetMember(memberAddress);
        }

        StartPartitionPull(_currentTopology!, incomplete, context);
        Logger.LogWarning("[PartitionIdentity] Incomplete rebalance detected, will retry {@Addresses}", incomplete);

        return false;
    }

    private HashSet<string> GetIncompletePartitionAddresses(HandoverSink sink, string address)
    {
        var incomplete = new HashSet<string>();

        foreach (var partition in sink.CompletedHandovers)
        {
            var localCount = _memberStats.GetActivationCount(partition.Address);
            var activatorCount = partition.TotalActivations;

            if (localCount != activatorCount)
            {
                incomplete.Add(partition.Address);
            }
        }

        return incomplete;
    }

    private Task OnStarted(IContext context)
    {
        var self = context.Self;
        _cluster.System.EventStream.Subscribe<ActivationTerminated>(context.System.Root, context.Self);

        return Task.CompletedTask;
    }

    private Task OnClusterTopology(ClusterTopology msg, IContext context)
    {
        if (TopologyHash.Equals(msg.TopologyHash))
        {
            return Task.CompletedTask;
        }

        FailSpawnsTargetingLeftMembers(msg);
        SetTopology(msg);

        if (msg.Members.Count == 0)
        {
            Logger.LogWarning("[PartitionIdentity] No active members in cluster topology update");
            _partitionLookup.Clear();
            _memberStats.Clear();

            return Task.CompletedTask;
        }

        SetReadyToRebalanceIfNoMoreWaitingSpawns();
        DiscardInvalidatedActivations();

        _rebalanceTcs ??= new TaskCompletionSource<ulong>();
        _currentHandover = new HandoverSink(msg, TakeOverIdentities(context));
        _rebalanceTimer = Stopwatch.StartNew();

        Logger.LogInformation(
            "{SystemId} topology {CurrentTopology} Pausing activations while rebalance in progress, {SpawnCount} spawns waiting",
            _cluster.System.Id, TopologyHash, _spawns.Count
        );

        if (_config.Mode == PartitionIdentityLookup.Mode.Push) // Good things comes to those who wait
        {
            return Task.CompletedTask;
        }

        var timer = Stopwatch.StartNew();

        var topologyValidityToken = msg.TopologyValidityToken!.Value;

        var waitUntilInFlightActivationsAreCompleted =
            _cluster.Gossip.WaitUntilInFlightActivationsAreCompleted(_config.RebalanceActivationsCompletionTimeout,
                topologyValidityToken);

        context.ReenterAfter(waitUntilInFlightActivationsAreCompleted, consensusResult =>
            {
                if (TopologyHash != msg.TopologyHash || topologyValidityToken.IsCancellationRequested)
                {
                    // Cancelled
                    return Task.CompletedTask;
                }

                timer.Stop();
                var allNodesCompletedActivations = consensusResult.Result.consensus;

                if (allNodesCompletedActivations)
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug(
                            "[PartitionIdentity] {SystemId} All nodes OK, Initiating rebalance:, {CurrentTopology} {ConsensusHash} after {Duration}",
                            _cluster.System.Id, TopologyHash, consensusResult.Result.topologyHash, timer.Elapsed
                        );
                    }
                }
                else
                {
                    Logger.LogError(
                        "[PartitionIdentity] {SystemId} Consensus not reached, Initiating rebalance:, {CurrentTopology} {ConsensusHash} after {Duration}",
                        _cluster.System.Id, TopologyHash, consensusResult.Result.topologyHash, timer.Elapsed
                    );
                }

                StartPartitionPull(msg, msg.Members.Select(it => it.Address), context, _deltaTopology);

                return Task.CompletedTask;
            }
        );

        return Task.CompletedTask;
    }

    private Action<IdentityHandover> TakeOverIdentities(IContext context) =>
        handover =>
        {
            foreach (var activation in handover.Actors)
            {
                TakeOverIdentity(activation.ClusterIdentity, activation.Pid, context);
            }
        };

    private void DiscardInvalidatedActivations()
    {
        var members = _currentMemberAddresses;

        var invalid = _partitionLookup
            .Where(kv => !members.Contains(kv.Value.Address) ||
                         !_memberHashRing.GetOwnerMemberByIdentity(kv.Key)
                             .Equals(_myAddress, StringComparison.InvariantCultureIgnoreCase)
            )
            .ToList();

        foreach (var (clusterIdentity, pid) in invalid)
        {
            _partitionLookup.Remove(clusterIdentity);
            _memberStats.Dec(pid.Address);
        }
    }

    private void DiscardActivationsByMemberAddresses(HashSet<string> memberAddressesToRemove)
    {
        foreach (var address in memberAddressesToRemove)
        {
            _memberStats.Remove(address);
        }

        var invalid = _partitionLookup
            .Where(kv => memberAddressesToRemove.Contains(kv.Value.Address))
            .ToList();

        foreach (var (clusterIdentity, _) in invalid)
        {
            _partitionLookup.Remove(clusterIdentity);
        }
    }

    private void SetTopology(ClusterTopology msg)
    {
        _currentTopology = msg;
        _memberHashRing = new MemberHashRing(msg.Members);
        _currentMemberAddresses = msg.Members.Select(it => it.Address).ToHashSet();
    }

    private void FailSpawnsTargetingLeftMembers(ClusterTopology topology)
    {
        if (topology.Left.Count == 0)
        {
            return;
        }

        var leftAddresses = topology.Left.Select(member => member.Address).ToHashSet();

        var spawningOnLeftMembers = _spawns.Where(it => leftAddresses.Contains(it.Value.activationAddress)).ToList();

        if (spawningOnLeftMembers.Count == 0)
        {
            return;
        }

        var result = new ActivationResponse
        {
            Failed = true
        };

        foreach (var (clusterIdentity, invalidSpawn) in spawningOnLeftMembers)
        {
            invalidSpawn.Response.TrySetResult(result);
            _spawns.Remove(clusterIdentity);
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionIdentity] Removed {Count} spawns targeting previous members",
                spawningOnLeftMembers.Count);
        }
    }

    private void SetReadyToRebalanceIfNoMoreWaitingSpawns()
    {
        if (_spawns.Count == 0)
        {
            _cluster.Gossip.SetInFlightActivationsCompleted(TopologyHash);
        }
    }

    private void StartPartitionPull(
        ClusterTopology msg,
        IEnumerable<string> memberAddresses,
        IContext context,
        ClusterTopology? deltaBaseline = null
    )
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            if (deltaBaseline is not null)
            {
                Logger.LogInformation(
                    "[PartitionIdentity] Pulling activations between topology {PrevTopology} and {CurrentTopology} from {@MemberAddresses}",
                    deltaBaseline.TopologyHash, msg.TopologyHash, memberAddresses
                );
            }
            else
            {
                Logger.LogInformation(
                    "[PartitionIdentity] Pulling activations for topology {CurrentTopology} from {@MemberAddresses}",
                    msg.TopologyHash,
                    memberAddresses
                );
            }
        }

        var workerPid = SpawnRebalanceWorker(memberAddresses, context, msg.TopologyValidityToken!.Value);

        context.Request(workerPid, new IdentityHandoverRequest
            {
                Address = _myAddress,
                CurrentTopology = new IdentityHandoverRequest.Types.Topology
                {
                    TopologyHash = TopologyHash,
                    Members = { msg.Members }
                },
                // If we have a known good topology rebalance, we can let it just rebalance the difference (delta) between the topologies
                DeltaTopology = deltaBaseline is not null
                    ? new IdentityHandoverRequest.Types.Topology
                    {
                        TopologyHash = deltaBaseline.TopologyHash,
                        Members = { deltaBaseline.Members }
                    }
                    : null
            }
        );
    }

    private PID SpawnRebalanceWorker(IEnumerable<string> rebalanceTargetAddresses, IContext context,
        CancellationToken cancellationToken) =>
        context.Spawn(
            Props.FromProducer(()
                => new PartitionIdentityRebalanceWorker(rebalanceTargetAddresses, _config.RebalanceRequestTimeout,
                    cancellationToken)
            )
        );

    private void TakeOverIdentity(ClusterIdentity clusterIdentity, PID activation, IContext context)
    {
        if (_partitionLookup.TryAdd(clusterIdentity, activation))
        {
            _memberStats.Inc(activation.Address);

            return;
        }

        var existingActivation = _partitionLookup[clusterIdentity];

        if (!existingActivation.Equals(activation))
        {
            ResolveDuplicateActivations(clusterIdentity, existingActivation, activation, context);
        }
    }

    private void ResolveDuplicateActivations(ClusterIdentity clusterIdentity, PID existingActivation,
        PID conflictingActivation, IContext context)
    {
        Logger.LogError(
            "[PartitionIdentity] Got duplicate activations of {ClusterIdentity}: {ExistingActivation}, {NewActivation}, terminating the previous activation",
            clusterIdentity,
            existingActivation,
            conflictingActivation
        );

        // Could possibly reach out to both of them and check liveness, but this kind of double-activation should not happen in normal operations.
        // Since the conflicting activation has reported last, we assume it is live and replace the existing one
        context.Stop(existingActivation);
        _partitionLookup[clusterIdentity] = conflictingActivation;
    }

    private Task OnActivationTerminated(ActivationTerminated msg)
    {
        if (_spawns.ContainsKey(msg.ClusterIdentity))
        {
            return Task.CompletedTask;
        }

        //we get this via broadcast to all nodes, remove if we have it, or ignore
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Terminated {Pid}", msg.Pid);
        }

        if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var existingActivation) &&
            existingActivation.Equals(msg.Pid))
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
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Found {Pid} for {ClusterIdentity} in local lookup",
                    pid, msg.ClusterIdentity);
            }

            context.Respond(new ActivationResponse { Pid = pid });

            return Task.CompletedTask;
        }

        // Wait for rebalance in progress
        if (_rebalanceTcs is not null)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Waiting for rebalance to complete for {ClusterIdentity}",
                    msg.ClusterIdentity);
            }

            context.ReenterAfter(_rebalanceTcs.Task, _ => OnActivationRequest(msg, context));

            return Task.CompletedTask;
        }

        if (_memberHashRing.Count == 0)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] No members in cluster for {ClusterIdentity}",
                    msg.ClusterIdentity);
            }
            RespondWithFailure(context);

            return Task.CompletedTask;
        }

        if (msg.TopologyHash != TopologyHash)
        {
            var ownerAddress = _memberHashRing.GetOwnerMemberByIdentity(msg.Identity);

            if (ownerAddress != _myAddress)
            {
                var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
                Logger.LogWarning("[PartitionIdentity] Tried to spawn on wrong node, forwarding");
                context.Forward(ownerPid);

                return Task.CompletedTask;
            }
        }

        //Get activator
        var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

        if (string.IsNullOrEmpty(activatorAddress))
        {
            //No activator currently available, return unavailable
            Logger.LogWarning("[PartitionIdentity] No members currently available for kind {Kind}", msg.Kind);
            RespondWithFailure(context);

            return Task.CompletedTask;
        }

        //What is this?
        //in case the actor of msg.Name is not yet spawned. there could be multiple re-entrant
        //messages requesting it, we just reuse the same task for all those
        //once spawned, the key is removed from this dict
        if (_spawns.TryGetValue(msg.ClusterIdentity, out var res))
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Found {Pid} for {ClusterIdentity} in pending lookup, waiting for spawn",
                    res, msg.ClusterIdentity);
            }
            // Just waits for the already in-progress activation to complete (or fail)
            context.ReenterAfter(res.Response.Task, task =>
                {
                    context.Respond(task.Result);

                    return Task.CompletedTask;
                }
            );

            return Task.CompletedTask;
        }

        // Not in progress, spawn actor

        var spawnResponse = SpawnRemoteActor(context, msg, activatorAddress);
        var setResponse = new TaskCompletionSource<ActivationResponse>();
        _spawns.Add(msg.ClusterIdentity, (setResponse, activatorAddress));

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Spawning {ClusterIdentity} on {ActivatorAddress}",
                msg.ClusterIdentity, activatorAddress);
        }
        //execution ends here. context.ReenterAfter is invoked once the task completes
        //but still within the actors sequential execution
        //but other messages could have been processed in between

        //Await SpawningProcess
        context.ReenterAfter(spawnResponse, OnSpawnResponse(msg, context, setResponse));

        return Task.CompletedTask;
    }

    private Func<Task<ActivationResponse>, Task> OnSpawnResponse(
        ActivationRequest msg,
        IContext context,
        TaskCompletionSource<ActivationResponse> setResponse
    ) =>
        async rst =>
        {
            try
            {
                var response = await rst.ConfigureAwait(false);

                if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var pid))
                {
                    if (response.Pid is not null && !response.Pid.Equals(pid))
                    {
                        context.Stop(response.Pid); // Stop duplicate activation
                    }

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Found {Pid} for {ClusterIdentity} in local lookup",
                            pid, msg.ClusterIdentity);
                    }

                    Respond(new ActivationResponse { Pid = pid, TopologyHash = TopologyHash });

                    return;
                }

                if (response.Pid != null)
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("[PartitionIdentity] [PartitionIdentityActor] Spawned {ClusterIdentity} on {Pid}",
                            msg.ClusterIdentity, response.Pid);
                    }
                    
                    if (response.Failed is false)
                    {
                        if (response.TopologyHash != TopologyHash) // Topology changed between request and response
                        {
                            if (!_currentMemberAddresses.Contains(response.Pid.Address))
                            {
                                // No longer part of cluster, dropped
                                Logger.LogWarning(
                                    "[PartitionIdentity] Received activation response {@Response}, no longer part of cluster",
                                    response);

                                Respond(new ActivationResponse { Failed = true });

                                return;
                            }

                            var currentActivatorAddress =
                                _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

                            if (_myAddress != currentActivatorAddress)
                            {
                                //Stop it or handover. ? Should be rebalanced in the current pass
                                Logger.LogWarning(
                                    "[PartitionIdentity] Misplaced spawn: {ClusterIdentity}, {Pid}, Expected {MyAddress} ({MyTopology}), Actual {ActivatorAddress} ({ActivatorTopology})",
                                    msg.ClusterIdentity, response.Pid, _myAddress, TopologyHash,
                                    currentActivatorAddress, response.TopologyHash);
                            }
                        }
                    }

                    _partitionLookup[msg.ClusterIdentity] = response.Pid;
                    _memberStats.Inc(response.Pid.Address);
                    Respond(response);

                    return;
                }

                // Failed, return err response
                Respond(response);
            }
            catch (Exception x)
            {
                x.CheckFailFast();
                Logger.LogError(x, "[PartitionIdentity] Spawn failed");
                _deltaTopology = null; // Do not use delta handover if we are not sure all spawns are OK.
                Respond(new ActivationResponse { Failed = true });
            }
            finally
            {
                var wasPresent = _spawns.Remove(msg.ClusterIdentity);

                if (wasPresent && _rebalanceTcs is not null && _spawns.Count == 0)
                {
                    SetReadyToRebalanceIfNoMoreWaitingSpawns();
                }
            }

            // The response both responds to the initial activator, but also any other waiting reentrant requests
            void Respond(ActivationResponse response)
            {
                context.Respond(response);
                setResponse.TrySetResult(response);
            }
        };

    private static void RespondWithFailure(IContext context) =>
        context.Respond(new ActivationResponse { Failed = true });

    private async Task<ActivationResponse> SpawnRemoteActor(IContext context, ActivationRequest req,
        string activatorAddress)
    {
        try
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("[PartitionIdentity] Spawning Remote Actor {Activator} {Identity} {Kind}",
                    activatorAddress, req.Identity, req.Kind);
            }

            var timeout = _cluster.Config.ActorActivationTimeout;
            var activatorPid = PartitionManager.RemotePartitionPlacementActor(activatorAddress);

            var res = await context.RequestAsync<ActivationResponse>(activatorPid, req, timeout).ConfigureAwait(false);

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

    private class MemberStatistics
    {
        private readonly Dictionary<string, MemberDetails> _stats = new();

        public IReadOnlyDictionary<string, MemberDetails> Members => _stats;

        public void Clear() => _stats.Clear();

        public void Inc(string memberAddress)
        {
            if (_stats.TryGetValue(memberAddress, out var item))
            {
                item.Activations++;
            }
            else
            {
                _stats[memberAddress] = new MemberDetails
                {
                    Activations = 1
                };
            }
        }

        public void Dec(string memberAddress)
        {
            if (_stats.TryGetValue(memberAddress, out var item))
            {
                item.Activations--;
            }
        }

        public int GetActivationCount(string memberAddress) =>
            _stats.TryGetValue(memberAddress, out var item) ? item.Activations : 0;

        public void Remove(string memberAddress) => _stats.Remove(memberAddress);

        public class MemberDetails
        {
            public int Activations { get; set; }
        }
    }

    private enum OperatingState
    {
        NoTopology,
        Normal,
        CompletingSpawns,
        ReBalancing
    }
}

public record PartitionCompleted(string MemberAddress, List<IdentityHandover> Chunks);

public record PartitionFailed(string MemberAddress, string Reason);