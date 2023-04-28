// -----------------------------------------------------------------------
// <copyright file="TopologyRelocationActor.cs" company="Asynkron AB">
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

namespace Proto.Cluster.Partition;

/// <summary>
///     Used by partitionIdentityActor to update partition lookup on topology changes.
///     Delegates each member to a separate worker actor which handles chunked transfers and can be retried on a member
///     basis.
/// </summary>
internal class PartitionIdentityRebalanceWorker : IActor, IDisposable
{
    private const string ReasonTimeout = "Timeout";
    private const string ReasonDeadletter = "DeadLetter";

    private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();
    private readonly CancellationToken _cancellationToken;
    private readonly TimeSpan _handoverTimeout;

    private readonly HashSet<string> _remainingPartitions;
    private PID? _partitionIdentityPid;
    private IdentityHandoverRequest? _request;
    private Stopwatch? _timer;
    private CancellationTokenRegistration? _tokenRegistration;

    /// <summary>
    ///     Worker which is responsible to pull activations for a given member-set.
    /// </summary>
    /// <param name="targetMemberAddresses">Addresses it should target, normally all active topology members</param>
    /// <param name="handoverTimeout">Retry a partition if it does not receive a response within this timeout</param>
    /// <param name="cancellationToken">Cancels the handover, does not send any more messages</param>
    public PartitionIdentityRebalanceWorker(
        IEnumerable<string> targetMemberAddresses,
        TimeSpan handoverTimeout,
        CancellationToken cancellationToken
    )
    {
        _remainingPartitions = targetMemberAddresses.ToHashSet();
        _handoverTimeout = handoverTimeout;
        _cancellationToken = cancellationToken;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            IdentityHandoverRequest request => OnIdentityHandoverRequest(request, context),
            PartitionCompleted response     => OnPartitionCompleted(response, context),
            PartitionFailed response        => OnPartitionFailed(response, context),
            _                               => Task.CompletedTask
        };

    public void Dispose() => _tokenRegistration?.Dispose();

    private Task OnIdentityHandoverRequest(IdentityHandoverRequest request, IContext context)
    {
        _timer = Stopwatch.StartNew();
        _partitionIdentityPid = context.Sender;

        _tokenRegistration = _cancellationToken.Register(() => context.Self.Stop(context.System)
        );

        _request = request;

        foreach (var member in request.CurrentTopology.Members)
        {
            var memberAddress = member.Address;
            StartRebalanceFromMember(request, context, memberAddress);
        }

        return Task.CompletedTask;
    }

    private Task OnPartitionCompleted(PartitionCompleted response, IContext context)
    {
        context.Send(_partitionIdentityPid!, response);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[PartitionIdentity] Completed pulling partition {Address}, {ChunkCount} chunks received",
                response.MemberAddress, response.Chunks.Count
            );
        }

        _remainingPartitions.Remove(response.MemberAddress);

        if (_remainingPartitions.Count == 0)
        {
            Logger.LogInformation(
                "[PartitionIdentity] Pulled activations from {MemberCount} partitions completed in {Elapsed}",
                _request!.CurrentTopology.Members.Count,
                _timer!.Elapsed
            );

            context.Self.Stop(context.System);
        }

        return Task.CompletedTask;
    }

    private Task OnPartitionFailed(PartitionFailed response, IContext context)
    {
        switch (response.Reason)
        {
            case ReasonTimeout:
                Logger.LogWarning("[PartitionIdentity] Partition {Member} timed out, retrying", response.MemberAddress);
                StartRebalanceFromMember(_request!, context, response.MemberAddress);

                break;

            case ReasonDeadletter:
            default:
                Logger.LogWarning("[PartitionIdentity] Partition {Member} unreachable", response.MemberAddress);

                context.ReenterAfter(Task.Delay(200, _cancellationToken),
                    () => StartRebalanceFromMember(_request!, context, response.MemberAddress)
                );

                break;
        }

        return Task.CompletedTask;
    }

    private void StartRebalanceFromMember(IdentityHandoverRequest request, IContext context, string memberAddress)
    {
        var childPid = context.Spawn(Props.FromProducer(() => new PartitionWorker(memberAddress, _handoverTimeout)));
        context.Request(childPid, request);
        _remainingPartitions.Add(memberAddress);
    }

    /// <summary>
    ///     Handles a single member rebalance.
    ///     Split out to make sure a failure against a single member can be retried without affecting the rest.
    /// </summary>
    private class PartitionWorker : IActor
    {
        private readonly List<IdentityHandover> _chunks = new();
        private readonly TaskCompletionSource<object> _completionSource = new();
        private readonly string _memberAddress;
        private readonly PID _targetMember;
        private readonly TimeSpan _timeout;
        private MemberHandoverSink? _sink;

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
                case Started:
                    _sink = new MemberHandoverSink(_memberAddress, handover => _chunks.Add(handover));

                    break;
                case IdentityHandoverRequest msg:
                    OnIdentityHandoverRequest(msg, context);

                    break;
                case IdentityHandover msg:
                    OnIdentityHandover(msg, context);

                    break;
                case ReceiveTimeout:
                    FailPartition(ReasonTimeout);

                    break;
                case DeadLetterResponse:
                    FailPartition(ReasonDeadletter);

                    break;
            }

            return Task.CompletedTask;
        }

        private void OnIdentityHandoverRequest(IdentityHandoverRequest msg, IContext context)
        {
            context.Request(_targetMember, msg);
            context.SetReceiveTimeout(_timeout);

            context.ReenterAfter(_completionSource.Task, () =>
                {
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
                    "[PartitionIdentity] Invalid IdentityHandover chunk {ChunkId} count {Count}, final {Final}, topology {TopologyHash} received, missing sender",
                    response.ChunkId, response.Actors.Count, response.Final, response.TopologyHash
                );
            }

            var complete = _sink!.Receive(response);

            if (!complete)
            {
                return;
            }

            _completionSource.SetResult(new PartitionCompleted(_memberAddress, _chunks));

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    "[PartitionIdentity] Final handover received from {Address}: skipped: {Skipped}/{Total}, chunks: {Chunks}",
                    _memberAddress,
                    _sink.SkippedActivations, _sink.SentActivations + _sink.SkippedActivations, response.ChunkId
                );
            }
        }

        private void FailPartition(string reason) =>
            _completionSource.TrySetResult(new PartitionFailed(_memberAddress, reason));
    }
}