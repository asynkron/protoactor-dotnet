// -----------------------------------------------------------------------
// <copyright file="PartitionPlacementActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.Partition
{
    class PartitionPlacementActor : IActor, IDisposable
    {
        private readonly Cluster _cluster;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionPlacementActor>();

        //pid -> the actor that we have created here
        //kind -> the actor kind
        private readonly Dictionary<ClusterIdentity, PID> _myActors = new();
        private readonly PartitionConfig _config;
        private EventStreamSubscription<object>? _subscription;

        private ClusterTopology? _lastRebalancedTopology;

        public PartitionPlacementActor(Cluster cluster, PartitionConfig config)
        {
            _cluster = cluster;
            _config = config;
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started                     => OnStarted(context),
                ActivationTerminating msg   => ActivationTerminating(msg),
                IdentityHandoverRequest msg => IdentityHandoverRequest(context, msg),
                ClusterTopology msg         => OnClusterTopology(context, msg),
                ActivationRequest msg       => ActivationRequest(context, msg),
                _                           => Task.CompletedTask
            };

        private Task OnClusterTopology(IContext context, ClusterTopology msg)
        {
            if (_config.Mode != PartitionIdentityLookup.Mode.Push) return Task.CompletedTask;

            Logger.LogDebug("Got topology {TopologyHash}, waiting for current activations to complete", msg.TopologyHash);

            var cancellationToken = msg.TopologyValidityToken!.Value;
            // TODO: Configurable timeout
            var activationsCompleted = _cluster.Gossip.WaitUntilInFlightActivationsAreCompleted(TimeSpan.FromSeconds(10), cancellationToken);

            // Waits until all members agree on a cluster topology and have no more in-flight activation requests
            context.ReenterAfter(activationsCompleted, async _ => {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Rebalance(context, msg);
                    }
                }
            );
            return Task.CompletedTask;
        }

        private async Task Rebalance(IContext context, ClusterTopology msg)
        {
            Logger.LogDebug("Initiating rebalance publish for topology {TopologyHash}", msg.TopologyHash);

            try
            {
                var handoverStates = new Dictionary<string, MemberHandover>();

                foreach (var member in msg.Members)
                {
                    handoverStates[member.Id] = new MemberHandover(context, member, msg, _config);
                }

                var currentHashRing = new MemberHashRing(msg.Members);

                switch (_config.Send)
                {
                    case PartitionIdentityLookup.Send.Delta:
                        var previousHashRing = _lastRebalancedTopology is null ? null : new MemberHashRing(_lastRebalancedTopology.Members);

                        foreach (var (clusterIdentity, activation) in _myActors)
                        {
                            var identity = clusterIdentity.Identity;
                            var owner = currentHashRing.GetNode(identity);
                            var handoverState = handoverStates[owner.Id];

                            var previousOwner = previousHashRing?.GetNode(identity);

                            if (previousOwner?.Id.Equals(owner.Id, StringComparison.Ordinal) == true)
                            {
                                handoverState.AddSkipped();
                            }
                            else
                            {
                                handoverState.Add(clusterIdentity, activation);
                            }
                        }

                        break;

                    case PartitionIdentityLookup.Send.Everything:
                    default:
                        foreach (var (clusterIdentity, activation) in _myActors)
                        {
                            var identity = clusterIdentity.Identity;
                            var owner = currentHashRing.GetNode(identity);
                            var memberHandover = handoverStates[owner.Id];
                            memberHandover.Add(clusterIdentity, activation);
                        }

                        break;
                }

                var waitingRequests = handoverStates.Values.SelectMany(it => it.Complete()).ToList();
                await Task.WhenAll(waitingRequests);

                if (waitingRequests.All(task => task.IsCompletedSuccessfully && task.Result is not null))
                {
                    Logger.LogDebug("Completed rebalance publish for topology {TopologyHash}", msg.TopologyHash);

                    // All members should now be up-to-date with the current set of activations.
                    // With delta sends, it will use this to determine which activations to skip sending
                    _lastRebalancedTopology = msg;
                }
                else
                {
                    Logger.LogDebug("Cancelled rebalance after publish for topology {TopologyHash}", msg.TopologyHash);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Cancelled rebalance publish for topology {TopologyHash}", msg.TopologyHash);
            }
        }

        private class MemberHandover
        {
            private readonly List<Task<IdentityHandoverAck>> _responseTasks = new();
            private readonly PID _target;
            private readonly IContext _context;
            private readonly ClusterTopology _topology;
            private readonly int _chunkSize;
            private readonly TimeSpan _requestTimeout;

            private IdentityHandover _identityHandover;
            private uint _chunkId;
            private uint _skipped;

            public MemberHandover(IContext context, Member member, ClusterTopology msg, PartitionConfig config)
            {
                _context = context;
                _topology = msg;
                _chunkSize = config.HandoverChunkSize;
                _requestTimeout = config.RebalanceRequestTimeout;
                _target = PartitionManager.RemotePartitionIdentityActor(member.Address);
                _identityHandover = new IdentityHandover
                {
                    ChunkId = ++_chunkId,
                    TopologyHash = _topology.TopologyHash
                };
            }

            public void Add(ClusterIdentity id, PID activation)
            {
                if (_identityHandover.Actors.Count == _chunkSize)
                {
                    Flush();
                }

                _identityHandover.Actors.Add(new Activation
                    {
                        ClusterIdentity = id,
                        Pid = activation
                    }
                );
            }

            /// <summary>
            /// When sending delta only, includes the number of skipped messages (already present on the owner from last rebalance)
            /// </summary>
            public void AddSkipped() => _skipped++;

            private void Flush()
            {
                var cancellationToken = _topology.TopologyValidityToken!.Value;
                cancellationToken.ThrowIfCancellationRequested();

                SendWithRetries(_identityHandover, cancellationToken);
                _identityHandover = new IdentityHandover
                {
                    ChunkId = ++_chunkId,
                    TopologyHash = _topology.TopologyHash
                };
            }

            private void SendWithRetries(IdentityHandover identityHandover, CancellationToken cancellationToken)
            {
                var task = Retry.TryUntil(() => {
                        using var timeout = new CancellationTokenSource(_requestTimeout);
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
                        return _context.RequestAsync<IdentityHandoverAck>(_target, identityHandover, linked.Token);
                    },
                    ack => cancellationToken.IsCancellationRequested || ack is not null,
                    int.MaxValue // Continue until complete or cancelled,
                );
                _responseTasks.Add(task);
            }

            public IEnumerable<Task<IdentityHandoverAck>> Complete()
            {
                _identityHandover.Final = true;
                _identityHandover.Skipped = _skipped;
                SendWithRetries(_identityHandover, _topology.TopologyValidityToken!.Value);
                return _responseTasks;
            }
        }

        private Task OnStarted(IContext context)
        {
            _subscription = context.System.EventStream.Subscribe<ActivationTerminating>(e => context.Send(context.Self, e));
            return Task.CompletedTask;
        }

        private Task ActivationTerminating(ActivationTerminating msg)
        {
            if (!_myActors.TryGetValue(msg.ClusterIdentity, out var pid))
            {
                Logger.LogWarning("Activation not found: {ActivationTerminating}", msg);
                return Task.CompletedTask;
            }

            if (!pid.Equals(msg.Pid))
            {
                Logger.LogWarning("Activation did not match pid: {ActivationTerminating}, {Pid}", msg, pid);
                return Task.CompletedTask;
            }

            _myActors.Remove(msg.ClusterIdentity);

            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                ClusterIdentity = msg.ClusterIdentity,
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
        private Task IdentityHandoverRequest(IContext context, IdentityHandoverRequest msg)
        {
            var count = 0;
            var requestAddress = context.Sender!.Address;

            //use a local selector, which is based on the requesters view of the world
            var memberHashRing = new MemberHashRing(msg.Members);

            var chunk = 0;
            var response = new IdentityHandoverResponse
            {
                ChunkId = ++chunk,
                TopologyHash = msg.TopologyHash
            };
            var chunkSize = _config.HandoverChunkSize;
            using var cancelRebalance = new CancellationTokenSource();
            var outOfBandResponseHandler = context.System.Root.Spawn(AbortOnDeadLetter(cancelRebalance));

            try
            {
                foreach (var (clusterIdentity, pid) in _myActors)
                {
                    //who owns this identity according to the requesters memberlist?
                    var ownerAddress = memberHashRing.GetOwnerMemberByIdentity(clusterIdentity);

                    //this identity is not owned by the requester
                    if (ownerAddress != requestAddress) continue;

                    Logger.LogDebug("Transfer {Identity} to {NewOwnerAddress} -- {TopologyHash}", clusterIdentity, ownerAddress,
                        msg.TopologyHash
                    );

                    var actor = new Activation {ClusterIdentity = clusterIdentity, Pid = pid};
                    response.Actors.Add(actor);
                    count++;

                    if (count % chunkSize == 0)
                    {
                        if (cancelRebalance.IsCancellationRequested)
                        {
                            return Task.CompletedTask;
                        }

                        context.Request(context.Sender, response, outOfBandResponseHandler);
                        response = new IdentityHandoverResponse
                        {
                            ChunkId = ++chunk,
                            TopologyHash = msg.TopologyHash
                        };
                    }
                }

                if (cancelRebalance.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                response.Final = true;
                Logger.LogDebug(
                    "{Id}, Sending final response with {Count} activations, total {Total}, chunk {ChunkId}, {Pid}, Topology {TopologyHash}",
                    context.System.Id, response.Actors.Count, count, chunk,
                    outOfBandResponseHandler, msg.TopologyHash
                );

                context.Request(context.Sender, response);

                Logger.LogDebug("Transferred {Count} actor ownership to other members", count);
            }
            finally
            {
                if (cancelRebalance.IsCancellationRequested)
                {
                    if (_config.DeveloperLogging)
                    {
                        Console.WriteLine($"Cancelled rebalance handover for topology: {msg.TopologyHash}");
                    }

                    Logger.LogInformation("Cancelled rebalance: {@IdentityHandoverRequest}", msg);
                }

                context.Stop(outOfBandResponseHandler);
            }

            return Task.CompletedTask;
        }

        private Props AbortOnDeadLetter(CancellationTokenSource cts) => Props.FromFunc(responseContext => {
                // Node lost or rebalance cancelled because of topology changes
                if (responseContext.Message is DeadLetterResponse)
                {
                    cts.Cancel();
                }

                return Task.CompletedTask;
            }
        );

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            try
            {
                if (_myActors.TryGetValue(msg.ClusterIdentity, out var existing))
                {
                    if (_config.DeveloperLogging)
                        Console.WriteLine($"Activator got request for existing activation {msg.RequestId}");
                    //this identity already exists
                    var response = new ActivationResponse
                    {
                        Pid = existing,
                        TopologyHash = msg.TopologyHash
                    };
                    context.Respond(response);
                }
                else
                {
                    if (_config.DeveloperLogging)
                        Console.WriteLine($"Activator got request for new activation {msg.RequestId}");
                    var clusterKind = _cluster.GetClusterKind(msg.ClusterIdentity.Kind);
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here

                    var clusterProps = clusterKind.Props.WithClusterIdentity(msg.ClusterIdentity);

                    var pid = context.SpawnPrefix(clusterProps, msg.ClusterIdentity.Identity);

                    _myActors[msg.ClusterIdentity] = pid;

                    var response = new ActivationResponse
                    {
                        Pid = pid,
                        TopologyHash = msg.TopologyHash
                    };
                    context.Respond(response);
                    if (_config.DeveloperLogging)
                        Console.WriteLine($"Activated {msg.RequestId}");
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
                var response = new ActivationResponse
                {
                    Pid = null
                };
                context.Respond(response);
            }

            return Task.CompletedTask;
        }

        public void Dispose() => _subscription?.Unsubscribe();
    }
}