// -----------------------------------------------------------------------
// <copyright file="PartitionPlacementRebalance.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Logging;
using Proto.Utils;

namespace Proto.Cluster.Partition;

/// <summary>
/// Publishes rebalance information and manages identity handovers.
/// </summary>
internal sealed class PartitionPlacementRebalance
{
    private static readonly ILogger Logger = Log.CreateLogger<PartitionPlacementRebalance>();

    private readonly Cluster _cluster;
    private readonly PartitionConfig _config;
    private readonly Dictionary<ClusterIdentity, PID> _actors;
    private ClusterTopology? _lastRebalancedTopology;

    public PartitionPlacementRebalance(Cluster cluster, PartitionConfig config,
        Dictionary<ClusterIdentity, PID> actors)
    {
        _cluster = cluster;
        _config = config;
        _actors = actors;
    }

    public Task OnClusterTopology(IContext context, ClusterTopology msg)
    {
        if (_config.Mode != PartitionIdentityLookup.Mode.Push)
        {
            return Task.CompletedTask;
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionPlacementActor] Got topology {TopologyHash}, waiting for current activations to complete",
                msg.TopologyHash);
        }

        var cancellationToken = msg.TopologyValidityToken!.Value;

        var activationsCompleted = _cluster.Gossip
            .WaitUntilInFlightActivationsAreCompleted(_config.RebalanceActivationsCompletionTimeout, cancellationToken);

        context.ReenterAfter(activationsCompleted, async () =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await Rebalance(context, msg).ConfigureAwait(false);
            }
        });

        return Task.CompletedTask;
    }

    public Task OnIdentityHandoverRequest(IContext context, IdentityHandoverRequest msg)
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
            try
            {
                await Task
                    .WhenAll(waitingRequests)
                    .WaitAsync(TimeSpan.FromSeconds(30), context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // ignore timeout and continue processing
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation and continue processing
            }

            var allProcessed = true;

            foreach (var task in waitingRequests)
            {
                if (!task.IsCompletedSuccessfully)
                {
                    allProcessed = false;
                    break;
                }

                var ack = await task.ConfigureAwait(false);

                if (ack?.ProcessingState != IdentityHandoverAck.Types.State.Processed)
                {
                    allProcessed = false;
                    break;
                }
            }

            if (allProcessed)
            {
                Logger.LogInformation("[PartitionPlacementActor] Completed rebalance publish for topology {TopologyHash}",
                    msg.TopologyHash);

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

    private Props AbortOnDeadLetter(CancellationTokenSource cts) =>
        Props.FromFunc(responseContext =>
            {
                if (responseContext.Message is DeadLetterResponse)
                {
                    cts.Cancel();
                }

                return Task.CompletedTask;
            }
        );

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
                    catch (TimeoutException)
                    {
                        return null;
                    }
                },
                ack => cancellationToken.IsCancellationRequested || ack is not null,
                int.MaxValue
            );

            _responseTasks.Add(task);
        }
    }
}
