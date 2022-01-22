// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
        private readonly TimeSpan _gossipRequestTimeout;
        private readonly IGossipInternal _internal;

        // lookup from state key -> consensus checks

        public GossipActor(
            TimeSpan gossipRequestTimeout,
            string myId,
            Func<ImmutableHashSet<string>> getBlockedMembers,
            InstanceLogger? instanceLogger,
            int gossipFanout
        )
        {
            _gossipRequestTimeout = gossipRequestTimeout;
            _internal = new GossipInternal(myId, getBlockedMembers, instanceLogger, gossipFanout);
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState      => OnSetGossipStateKey(context,setState),
            GetGossipStateRequest getState  => OnGetGossipStateKey(context, getState),
            GossipRequest gossipRequest     => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest          => OnSendGossipState(context),
            AddConsensusCheck request       => OnAddConsensusCheck(request),
            RemoveConsensusCheck request    => OnRemoveConsensusCheck(request),
            ClusterTopology clusterTopology => OnClusterTopology(clusterTopology),
            _                               => Task.CompletedTask
        };

        private Task OnClusterTopology(ClusterTopology clusterTopology) =>
            _internal.UpdateClusterTopology(clusterTopology);

        private Task OnAddConsensusCheck(AddConsensusCheck msg)
        {
             _internal.AddConsensusCheck(msg.Check);
             return Task.CompletedTask;
        }

        private Task OnRemoveConsensusCheck(RemoveConsensusCheck request)
        {
            _internal.RemoveConsensusCheck(request.Id);
            return Task.CompletedTask;
        }

        private Task OnGetGossipStateKey(IContext context, GetGossipStateRequest getState)
        {
            var state = _internal.GetGossipStateKey(getState);
            var res = new GetGossipStateResponse(state);
            context.Respond(res);
            return Task.CompletedTask;
        }

        private Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            var logger = context.Logger()?.BeginScope<GossipActor>();
            logger?.LogDebug("Gossip Request {Sender}", context.Sender!);
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
            MergeState(context, gossipRequest.State);
            var senderMember = _internal.TryGetSenderMember(context.Sender!.Address);

            if (senderMember is null || !TryGetStateForMember(senderMember, out var pendingOffsets, out var stateForMember))
            {
                // Nothing to send, do not provide sender or state payload
                context.Respond(new GossipResponse());
                return Task.CompletedTask;
            }

            context.RequestReenter<GossipResponseAck>(context.Sender!, new GossipResponse
                {
                    State = stateForMember
                }, task => ReenterAfterResponseAck(context, task, pendingOffsets), context.CancellationToken
            );

            return Task.CompletedTask;
        }

        private void MergeState(IContext context, GossipState remoteState)
        {
            var updates = _internal.MergeState(remoteState);

            foreach (var update in updates)
            {
                context.System.EventStream.Publish(update);
            }
        }

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var (key, message) = setStateKey;
            _internal.SetState(key, message);
            context.Respond(new SetGossipStateResponse());
            return Task.CompletedTask;
        }

        private Task OnSendGossipState(IContext context)
        {
            _internal.SendState((m, l) => SendGossipForMember(context, m, l));
            context.Respond(new SendGossipStateResponse());
            return Task.CompletedTask;
        }

        private void SendGossipForMember(IContext context, Member member, InstanceLogger? logger)
        {
            var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);
            if (!TryGetStateForMember(member, out var pendingOffsets, out var stateForMember)) return;

            logger?.LogInformation("Sending GossipRequest to {MemberId}", member.Id);
            Logger.LogDebug("Sending GossipRequest to {MemberId}", member.Id);

            //a short timeout is massively important, we cannot afford hanging around waiting for timeout, blocking other gossips from getting through

            // This will return a GossipResponse, but since we need could need to get the sender, we do not unpack it from the MessageEnvelope
            var t = context.RequestAsync<MessageEnvelope>(pid, new GossipRequest
                {
                    State = stateForMember,
                }, CancellationTokens.WithTimeout(_gossipRequestTimeout)
            );

            context.ReenterAfter(t, task => GossipReenterAfterSend(context, task, pendingOffsets));
        }

        private bool TryGetStateForMember(Member member, out ImmutableDictionary<string, long> pendingOffsets, out GossipState stateForMember) =>
            _internal.TryGetMemberState(member, out pendingOffsets, out stateForMember);

        private async Task GossipReenterAfterSend(IContext context, Task<MessageEnvelope> task, ImmutableDictionary<string, long> pendingOffsets)
        {
            var logger = context.Logger();

            try
            {
                await task;
                var envelope = task.Result;

                if (envelope.Message is GossipResponse response)
                {
                    _internal.CommitPendingOffsets(pendingOffsets);

                    if (response.State is not null)
                    {
                        MergeState(context, response.State!);

                        if (envelope.Sender is not null)
                        {
                            context.Send(envelope.Sender, new GossipResponseAck());
                        }
                    }
                }
            }
            catch (DeadLetterException)
            {
                logger?.LogWarning("DeadLetter");
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (TimeoutException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (Exception x)
            {
                logger?.LogError(x, "OnSendGossipState failed");
                Logger.LogError(x, "OnSendGossipState failed");
            }
        }

        private async Task ReenterAfterResponseAck(IContext context, Task<GossipResponseAck> task, ImmutableDictionary<string, long> pendingOffsets)
        {
            var logger = context.Logger();

            try
            {
                await task;
                _internal.CommitPendingOffsets(pendingOffsets);
            }
            catch (DeadLetterException)
            {
                logger?.LogWarning("DeadLetter");
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (TimeoutException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (Exception x)
            {
                logger?.LogError(x, "OnSendGossipState failed");
                Logger.LogError(x, "OnSendGossipState failed");
            }
        }
    }
}