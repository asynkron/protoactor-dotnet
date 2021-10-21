// -----------------------------------------------------------------------
// <copyright file="TopologyRelocationActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    /// <summary>
    /// Used by partitionIdentityActor to update partition lookup on topology changes.
    /// </summary>
    class PartitionIdentityRelocationWorker : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();

        private readonly Dictionary<ClusterIdentity, PID> _partitionLookup;
        private readonly TaskCompletionSource<bool> _onRelocationComplete = new();
        private readonly Dictionary<PID, MemberRequestState> _waitingRequests = new();
        private int _totalReceived;
        private readonly Stopwatch _timer = new();

        public PartitionIdentityRelocationWorker(Dictionary<ClusterIdentity, PID> partitionLookup) => _partitionLookup = partitionLookup;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            IdentityHandoverRequest request   => OnIdentityHandoverRequest(request, context),
            IdentityHandoverResponse response => OnIdentityHandoverResponse(response, context),
            DeadLetterResponse response       => OnDeadLetterResponse(response, context),
            ReceiveTimeout                    => OnReceiveTimeout(),
            _                                 => Task.CompletedTask
        };

        private Task OnReceiveTimeout()
        {
            // TODO: Retry strategy
            Logger.LogError("Relocation timed out");
            _onRelocationComplete.TrySetResult(false);
            return Task.CompletedTask;
        }

        private Task OnDeadLetterResponse(DeadLetterResponse response, IContext context)
        {
            if (_waitingRequests.Remove(response.Target))
            {
                Logger.LogError("Unreachable node: {Address}", response.Target.Address);
                TryCompleteRelocation();
            }

            return Task.CompletedTask;
        }

        private Task OnIdentityHandoverResponse(IdentityHandoverResponse response, IContext context)
        {
            var sender = context.Sender;

            if (sender is null)
            {
                // Invalid response, requires sender to be populated
                Logger.LogError("Invalid IdentityHandoverResponse received, missing sender");
                return Task.CompletedTask;
            }

            foreach (var activation in response.Actors)
            {
                TakeOwnership(activation);
            }

            TryCompleteRelocation(response, sender);

            return Task.CompletedTask;
        }

        private void TryCompleteRelocation(IdentityHandoverResponse response, PID sender)
        {
            if (!_waitingRequests.TryGetValue(sender, out var workerState))
            {
                Logger.LogWarning("Received unexpected IdentityHandoverResponse from {Sender}, chunk {ChunkId} with {ActorCount} actors", sender,
                    response.ChunkId, response.Actors.Count
                );
                return;
            }

            _totalReceived += response.Actors.Count;

            if (workerState.TryComplete(response))
            {
                _waitingRequests.Remove(sender!);
                Logger.LogDebug("Received ownership of {Count} actors from {MemberAddress}", workerState.ReceivedActors, sender.Address);

                TryCompleteRelocation();
            }
        }

        private void TryCompleteRelocation()
        {
            if (_waitingRequests.Count == 0)
            {
                Logger.LogInformation("IdentityRelocation completed, received {Count} actors in {Elapsed}", _totalReceived, _timer.Elapsed);
                _onRelocationComplete.TrySetResult(true);
            }
        }

        private Task OnIdentityHandoverRequest(IdentityHandoverRequest request, IContext context)
        {
            _timer.Start();
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));

            foreach (var member in request.Members)
            {
                var activatorPid = PartitionManager.RemotePartitionPlacementActor(member.Address);
                context.Request(activatorPid, request);
                _waitingRequests[activatorPid] = new MemberRequestState();
            }

            context.ReenterAfter(_onRelocationComplete.Task, task => {
                    context.Respond(new IdentityHandoverAcknowledgement
                        {
                            Count = _totalReceived
                        }
                    );
                    context.Self.Stop(context.System);
                    return Task.CompletedTask;
                }
            );
            return Task.CompletedTask;
        }

        private class MemberRequestState
        {
            private readonly HashSet<int> _received = new();
            private int? _finalChunk;
            public int ReceivedActors { get; private set; }

            /// <summary>
            /// Checks if all chunks has been received
            /// </summary>
            /// <param name="response"></param>
            /// <returns></returns>
            public bool TryComplete(IdentityHandoverResponse response)
            {
                if (_received.Contains(response.ChunkId))
                {
                    Logger.LogWarning("Chunk {ChunkId} already received", response.ChunkId);
                    return false;
                }

                _received.Add(response.ChunkId);
                ReceivedActors += response.Actors.Count;

                if (response.Final)
                {
                    _finalChunk = response.ChunkId;
                }

                return _finalChunk.HasValue && _received.Count == _finalChunk;
            }
        }

        private void TakeOwnership(Activation msg)
        {
            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var existing))
            {
                //these are the same, that's good, just ignore message
                if (existing.Address == msg.Pid.Address) return;
            }

            // Logger.LogDebug("Taking Ownership of: {Identity}, pid: {Pid}", msg.Identity, msg.Pid);
            _partitionLookup[msg.ClusterIdentity] = msg.Pid;
        }
    }
}