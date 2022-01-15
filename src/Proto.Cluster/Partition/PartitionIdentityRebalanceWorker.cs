// -----------------------------------------------------------------------
// <copyright file="TopologyRelocationActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    /// <summary>
    /// Used by partitionIdentityActor to update partition lookup on topology changes.
    /// Delegates each member to a separate worker actor which handles chunked transfers and can be retried on a member basis.
    /// </summary>
    class PartitionIdentityRebalanceWorker : IActor, IDisposable
    {
        private readonly TimeSpan _handoverTimeout;
        private readonly CancellationToken _cancellationToken;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();

        private readonly List<PartitionActivations> _completedPartitions = new();

        private readonly TaskCompletionSource<bool> _onRelocationComplete;
        private readonly Dictionary<string, PID> _waitingRequests = new();
        private readonly Stopwatch _timer = new();
        private IdentityHandoverRequest? _request;
        private CancellationTokenRegistration? _tokenRegistration;

        public PartitionIdentityRebalanceWorker(TimeSpan handoverTimeout, CancellationToken cancellationToken)
        {
            _handoverTimeout = handoverTimeout;
            _cancellationToken = cancellationToken;
            _onRelocationComplete = new TaskCompletionSource<bool>();
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            IdentityHandoverRequest request => OnIdentityHandoverRequest(request, context),
            PartitionActivations response     => OnPartitionCompleted(response),
            PartitionFailed response        => OnPartitionFailed(response, context),
            _                               => Task.CompletedTask
        };

        private Task OnIdentityHandoverRequest(IdentityHandoverRequest request, IContext context)
        {
            _tokenRegistration = _cancellationToken.Register(() => context.Self.Stop(context.System)
            );
            _timer.Start();
            _request = request;

            foreach (var member in request.CurrentTopology.Members)
            {
                var memberAddress = member.Address;
                StartRebalanceFromMember(request, context, memberAddress);
            }

            context.ReenterAfter(_onRelocationComplete.Task, () => {
                    context.Respond(new PartitionsRebalanced(_completedPartitions, _request.CurrentTopology.TopologyHash));
                    context.Self.Stop(context.System);
                }
            );
            return Task.CompletedTask;
        }

        private Task OnPartitionCompleted(PartitionActivations response)
        {
            _completedPartitions.Add(response);
            _waitingRequests.Remove(response.MemberAddress);

            if (_waitingRequests.Count == 0)
            {
                _timer.Stop();
                Logger.LogDebug("IdentityRelocation completed, received {Count} actors in {Elapsed}", _completedPartitions.Count, _timer.Elapsed);
                _onRelocationComplete.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        private Task OnPartitionFailed(PartitionFailed response, IContext context)
        {
            Logger.LogWarning("Retrying member {Member}, failed with {Reason}", response.MemberAddress, response.Reason);
            StartRebalanceFromMember(_request!, context, response.MemberAddress);
            return Task.CompletedTask;
        }

        private void StartRebalanceFromMember(IdentityHandoverRequest request, IContext context, string memberAddress)
        {
            var childPid = context.Spawn(Props.FromProducer(() => new PartitionWorker(memberAddress, _handoverTimeout)));
            context.Request(childPid, request);
            _waitingRequests[memberAddress] = childPid;
        }

        /// <summary>
        /// Handles a single member rebalance.
        /// Split out to make sure a failure against a single member can be retried without affecting the rest.
        /// </summary>
        private class PartitionWorker : IActor
        {
            private readonly PID _targetMember;
            private readonly IndexSet _receivedChunks = new();
            private readonly List<Activation> _receivedActivations = new();
            private readonly string _memberAddress;
            private readonly TimeSpan _timeout;
            private readonly TaskCompletionSource<object> _completionSource = new();
            private int? _finalChunk;

            public PartitionWorker(string memberAddress, TimeSpan timeout)
            {
                _memberAddress = memberAddress;
                _timeout = timeout;
                _targetMember = PartitionManager.RemotePartitionPlacementActor(_memberAddress);
            }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case IdentityHandoverRequest msg:
                        OnIdentityHandoverRequest(msg, context);
                        break;
                    case IdentityHandover msg:
                        OnIdentityHandover(msg, context);
                        break;
                    case ReceiveTimeout:
                        FailPartition("Timeout");
                        break;
                    case DeadLetterResponse:
                        FailPartition("DeadLetter");
                        break;
                }

                return Task.CompletedTask;
            }

            private void OnIdentityHandoverRequest(IdentityHandoverRequest msg, IContext context)
            {
                context.Request(_targetMember, msg);
                context.SetReceiveTimeout(_timeout);
                context.ReenterAfter(_completionSource.Task, () => {
                        context.Send(context.Parent!, _completionSource.Task.Result);
                        context.Stop(context.Self);
                    }
                );
            }

            private void OnIdentityHandover(IdentityHandover response, IContext context)
            {
                var sender = context.Sender;

                if (sender is null)
                {
                    // Invalid response, requires sender to be populated
                    Logger.LogError(
                        "Invalid IdentityHandover chunk {ChunkId} count {Count}, final {Final}, topology {TopologyHash} received, missing sender",
                        response.ChunkId, response.Actors.Count, response.Final, response.TopologyHash
                    );
                }

                _receivedActivations.AddRange(response.Actors);

                if (HasReceivedAllChunks(response))
                {
                    _completionSource.SetResult(new PartitionActivations(_memberAddress, _receivedActivations, response.Skipped));

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("Final handover received from {Address}: skipped: {Skipped}/{Total}, chunks: {Chunks}", _memberAddress,
                            response.Skipped, response.Skipped + response.Sent, response.ChunkId
                        );
                    }
                }
            }

            private void FailPartition(string reason) => _completionSource.TrySetResult(new PartitionFailed(_memberAddress, reason));

            private bool HasReceivedAllChunks(IdentityHandover response)
            {
                if (!_receivedChunks.TryAddIndex(response.ChunkId))
                {
                    Logger.LogWarning("Chunk {ChunkId} already received", response.ChunkId);
                    return false;
                }

                if (response.Final)
                {
                    _finalChunk = response.ChunkId;
                }

                return _finalChunk.HasValue && _receivedChunks.IsCompleteSet;
            }
        }

        public void Dispose() => _tokenRegistration?.Dispose();
    }
}