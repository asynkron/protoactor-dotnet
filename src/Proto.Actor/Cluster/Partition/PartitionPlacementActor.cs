// -----------------------------------------------------------------------
// <copyright file="PartitionPlacementActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Utils;

namespace Proto.Cluster.Partition;

internal class PartitionPlacementActor : IActor, IDisposable
{
    private static readonly ILogger Logger = Log.CreateLogger<PartitionPlacementActor>();

    //pid -> the actor that we have created here
    //kind -> the actor kind
    private readonly Dictionary<ClusterIdentity, PID> _actors = new();
    private readonly Cluster _cluster;
    private readonly PartitionConfig _config;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();

    private ClusterTopology? _lastRebalancedTopology;
    private EventStreamSubscription<object>? _subscription;

    public PartitionPlacementActor(Cluster cluster, PartitionConfig config)
    {
        _cluster = cluster;
        _config = config;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                     => OnStarted(context),
            ActivationTerminating msg   => OnActivationTerminating(msg),
            IdentityHandoverRequest msg => OnIdentityHandoverRequest(context, msg),
            ClusterTopology msg         => OnClusterTopology(context, msg),
            ActivationRequest msg       => OnActivationRequest(context, msg),
            _                           => Task.CompletedTask
        };

    public void Dispose() => _subscription?.Unsubscribe();

    private Task OnClusterTopology(IContext context, ClusterTopology msg)
    {
        if (_config.Mode != PartitionIdentityLookup.Mode.Push)
        {
            return Task.CompletedTask;
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                "[PartitionPlacementActor] Got topology {TopologyHash}, waiting for current activations to complete",
                msg.TopologyHash);
        }

        var cancellationToken = msg.TopologyValidityToken!.Value;

        // TODO: Configurable timeout
        var activationsCompleted =
            _cluster.Gossip.WaitUntilInFlightActivationsAreCompleted(TimeSpan.FromSeconds(10), cancellationToken);

        // Waits until all members agree on a cluster topology and have no more in-flight activation requests
        context.ReenterAfter(activationsCompleted, async _ =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Rebalance(context, msg).ConfigureAwait(false);
                }
            }
        );

        return Task.CompletedTask;
    }

    private async Task Rebalance(IContext context, ClusterTopology msg)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionPlacementActor] Initiating rebalance publish for topology {TopologyHash}",
                msg.TopologyHash);
        }

        try
        {
            var handoverStates = new Dictionary<string, MemberHandover>();

            foreach (var member in msg.Members)
            {
                handoverStates[member.Address] = new MemberHandover(context, member, msg, _config);
            }

            foreach (var handover in GetPushHandovers(msg))
            {
                handoverStates[handover.address].Send(handover.message);
            }

            var waitingRequests = handoverStates.Values.SelectMany(it => it.WaitingMessages).ToList();
            await Task.WhenAll(waitingRequests).WaitUpTo(TimeSpan.FromSeconds(30), context.CancellationToken).ConfigureAwait(false);

            // Ensure that we only update last rebalanced topology when all members have received the current activations
            if (waitingRequests.All(task
                    => task.IsCompletedSuccessfully &&
                       task.Result?.ProcessingState == IdentityHandoverAck.Types.State.Processed
                ))
            {
                Logger.LogInformation("[PartitionPlacementActor] Completed rebalance publish for topology {TopologyHash}",
                    msg.TopologyHash);

                // All members should now be up-to-date with the current set of activations.
                // With delta sends, it will use this to determine which activations to skip sending
                _lastRebalancedTopology = msg;
            }
            else
            {
                Logger.LogInformation(
                    "[PartitionPlacementActor] Cancelled rebalance after publish for topology {TopologyHash}",
                    msg.TopologyHash);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("[PartitionPlacementActor] Cancelled rebalance publish for topology {TopologyHash}",
                msg.TopologyHash);
        }
    }

    private IEnumerable<(string address, IdentityHandover message)> GetPushHandovers(ClusterTopology msg)
    {
        var currentHashRing = new MemberHashRing(msg.Members);

        string GetCurrentOwner(ClusterIdentity identity) => currentHashRing.GetOwnerMemberByIdentity(identity);

        switch (_config.Send)
        {
            case PartitionIdentityLookup.Send.Delta:
                var previousHashRing = _lastRebalancedTopology is null
                    ? null
                    : new MemberHashRing(_lastRebalancedTopology.Members);

                return HandoverSource.CreateHandovers(msg, _config.HandoverChunkSize, _actors, GetCurrentOwner,
                    previousHashRing is null ? null : identity => previousHashRing.GetOwnerMemberByIdentity(identity)
                );
            case PartitionIdentityLookup.Send.Full:
            default:
                return HandoverSource.CreateHandovers(msg, _config.HandoverChunkSize, _actors, GetCurrentOwner);
        }
    }

    private IEnumerable<IdentityHandover> GetPullHandovers(IdentityHandoverRequest request)
    {
        //use a local selector, which is based on the requesters view of the world
        var currentHashRing = new MemberHashRing(request.CurrentTopology.Members);
        var address = request.Address!;

        if (request.DeltaTopology is null)
        {
            return HandoverSource.CreateHandovers(request.CurrentTopology.TopologyHash, _config.HandoverChunkSize,
                _actors,
                identity => currentHashRing.GetOwnerMemberByIdentity(identity).Equals(address, StringComparison.Ordinal)
            );
        }

        var previousHashRing = new MemberHashRing(request.DeltaTopology.Members);

        return HandoverSource.CreateHandovers(request.CurrentTopology.TopologyHash, _config.HandoverChunkSize, _actors,
            identity => currentHashRing.GetOwnerMemberByIdentity(identity).Equals(address, StringComparison.Ordinal),
            identity => previousHashRing.GetOwnerMemberByIdentity(identity).Equals(address, StringComparison.Ordinal)
        );
    }

    private Task OnStarted(IContext context)
    {
        _subscription = context.System.EventStream.Subscribe<ActivationTerminating>(e => context.Send(context.Self, e));

        return Task.CompletedTask;
    }

    private Task OnActivationTerminating(ActivationTerminating msg)
    {
        if (!_actors.TryGetValue(msg.ClusterIdentity, out var pid))
        {
            Logger.LogWarning("[PartitionPlacementActor] Activation not found: {ActivationTerminating}", msg);

            return Task.CompletedTask;
        }

        if (!pid.Equals(msg.Pid))
        {
            Logger.LogWarning("[PartitionPlacementActor] Activation did not match pid: {ActivationTerminating}, {Pid}", msg,
                pid);

            return Task.CompletedTask;
        }

        _actors.Remove(msg.ClusterIdentity);

        var activationTerminated = new ActivationTerminated
        {
            Pid = pid,
            ClusterIdentity = msg.ClusterIdentity
        };

        _cluster.MemberList.BroadcastEvent(activationTerminated);

        // var ownerAddress = _rdv.GetOwnerMemberByIdentity(clusterIdentity.Identity);
        // var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
        //
        // context.Send(ownerPid, activationTerminated);
        return Task.CompletedTask;
    }

    //this is pure, we do not change any state or actually move anything
    //the requester also provide its own view of the world in terms of members
    //TLDR; we are not using any topology state from this actor itself
    private Task OnIdentityHandoverRequest(IContext context, IdentityHandoverRequest msg)
    {
        if (context.Sender is null)
        {
            Logger.LogError("[PartitionPlacementActor] IdentityHandoverRequest {Request} missing sender", msg);

            return Task.CompletedTask;
        }

        using var cancelRebalance = new CancellationTokenSource();
        var outOfBandResponseHandler = context.System.Root.Spawn(AbortOnDeadLetter(cancelRebalance));

        try
        {
            foreach (var handover in GetPullHandovers(msg))
            {
                if (cancelRebalance.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                context.Request(context.Sender, handover, outOfBandResponseHandler);

                if (handover.Final && Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogInformation(
                        "[PartitionPlacementActor] {Id}, Sending final response with {Count} activations, total {Total}, skipped {Skipped}, Chunk {Chunk}, Topology {TopologyHash}",
                        context.System.Id, handover.Actors.Count, handover.Sent, handover.Skipped, handover.ChunkId,
                        msg.CurrentTopology.TopologyHash
                    );
                }
            }
        }
        finally
        {
            if (cancelRebalance.IsCancellationRequested)
            {
                context.Logger()
                    ?.LogInformation(
                        "[PartitionPlacementActor] Cancelled rebalance handover for topology: {TopologyHash}",
                        msg.CurrentTopology.TopologyHash
                    );

                if (_config.DeveloperLogging)
                {
                    Console.WriteLine($"Cancelled rebalance handover for topology: {msg.CurrentTopology.TopologyHash}");
                }

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("[PartitionPlacementActor] Cancelled rebalance: {@IdentityHandoverRequest}", msg);
                }
            }

            context.Stop(outOfBandResponseHandler);
        }

        return Task.CompletedTask;
    }

    private Props AbortOnDeadLetter(CancellationTokenSource cts) =>
        Props.FromFunc(responseContext =>
            {
                // Node lost or rebalance cancelled because of topology changes
                if (responseContext.Message is DeadLetterResponse)
                {
                    cts.Cancel();
                }

                return Task.CompletedTask;
            }
        );

    private Task OnActivationRequest(IContext context, ActivationRequest msg)
    {

        
        if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionPlacementActor] Activation already exists: {ClusterIdentity}, {Pid}", msg.ClusterIdentity, existing);
            }
            
            //this identity already exists
            var response = new ActivationResponse
            {
                Pid = existing,
                TopologyHash = msg.TopologyHash
            };

            context.Respond(response);

            return Task.CompletedTask;
        }

        var clusterKind = _cluster.TryGetClusterKind(msg.Kind);

        if (clusterKind is null)
        {
            Logger.LogError("Failed to spawn {Kind}/{Identity}, kind not found for member", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse
            {
                Failed = true,
                TopologyHash = msg.TopologyHash
            });

            return Task.CompletedTask;
        }

        if (clusterKind.CanSpawnIdentity is not null)
        {
            // Needs to check if the identity is allowed to spawn
            VerifyAndSpawn(msg, context, clusterKind);
        }
        else
        {
            Spawn(msg, context, clusterKind);
        }

        return Task.CompletedTask;
    }

    private void VerifyAndSpawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        var clusterIdentity = msg.ClusterIdentity;

        if (_inFlightIdentityChecks.Contains(clusterIdentity))
        {
            Logger.LogError("[PartitionPlacementActor] Duplicate activation requests for {ClusterIdentity}", clusterIdentity);

            context.Respond(new ActivationResponse
                {
                    Failed = true,
                    TopologyHash = msg.TopologyHash
                }
            );
        }

        var canSpawn = clusterKind.CanSpawnIdentity!(msg.Identity,
            CancellationTokens.FromSeconds(_cluster.Config.ActorSpawnVerificationTimeout));

        if (canSpawn.IsCompleted)
        {
            OnSpawnDecided(msg, context, clusterKind, canSpawn.Result);

            return;
        }

        _inFlightIdentityChecks.Add(clusterIdentity);

        context.ReenterAfter(canSpawn.AsTask(), task =>
            {
                _inFlightIdentityChecks.Remove(clusterIdentity);

                if (task.IsCompletedSuccessfully)
                {
                    OnSpawnDecided(msg, context, clusterKind, task.Result);
                }
                else
                {
                    Logger.LogError("[PartitionPlacementActor] Error when checking {ClusterIdentity}", clusterIdentity);

                    context.Respond(new ActivationResponse
                        {
                            Failed = true,
                            TopologyHash = msg.TopologyHash
                        }
                    );
                }

                return Task.CompletedTask;
            }
        );
    }

    private void Spawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        try
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionPlacementActor] Spawning {Kind}/{Identity}", msg.Kind, msg.Identity);
            }
            var pid = context.Spawn(clusterKind.Props, ctx => ctx.Set(msg.ClusterIdentity));
            _actors.Add(msg.ClusterIdentity, pid);

            context.Respond(new ActivationResponse
                {
                    Pid = pid,
                    TopologyHash = msg.TopologyHash
                }
            );
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            Logger.LogError(e, "[PartitionPlacementActor] Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse
            {
                Failed = true,
                TopologyHash = msg.TopologyHash
            });
        }
    }

    private void OnSpawnDecided(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind,
        bool canSpawnIdentity)
    {
        if (canSpawnIdentity)
        {
            Spawn(msg, context, clusterKind);
        }
        else
        {
            context.Respond(new ActivationResponse
                {
                    Failed = true,
                    InvalidIdentity = true,
                    TopologyHash = msg.TopologyHash
                }
            );
        }
    }

    private class MemberHandover
    {
        private readonly IContext _context;
        private readonly TimeSpan _requestTimeout;
        private readonly List<Task<IdentityHandoverAck?>> _responseTasks = new();
        private readonly PID _target;
        private readonly ClusterTopology _topology;

        public MemberHandover(IContext context, Member member, ClusterTopology msg, PartitionConfig config)
        {
            _context = context;
            _topology = msg;
            _requestTimeout = config.RebalanceRequestTimeout;
            _target = PartitionManager.RemotePartitionIdentityActor(member.Address);
        }

        public IEnumerable<Task<IdentityHandoverAck?>> WaitingMessages => _responseTasks;

        public void Send(IdentityHandover identityHandover) =>
            SendWithRetries(identityHandover, _topology.TopologyValidityToken!.Value);

        private void SendWithRetries(IdentityHandover identityHandover, CancellationToken cancellationToken)
        {
            var task = Retry.TryUntil(async () =>
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(_requestTimeout);

                        using var linked =
                            CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

                        return await _context.RequestAsync<IdentityHandoverAck>(_target, identityHandover,
                            linked.Token).ConfigureAwait(false);
                    }
                    catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
                    {
                        if (Logger.IsEnabled(LogLevel.Warning))
                        {
                            Logger.LogWarning(
                                "[PartitionPlacementActor] Identity handover request timeout for topology {TopologyHash}, address {Address}",
                                _topology.TopologyHash, _target.Address
                            );
                        }

                        return null;
                    }
                    catch (TimeoutException) // Cancelled, new topology active
                    {
                        return null;
                    }
                },
                ack => cancellationToken.IsCancellationRequested || ack is not null,
                int.MaxValue // Continue until complete or cancelled,
            );

            _responseTasks.Add(task);
        }
    }
}