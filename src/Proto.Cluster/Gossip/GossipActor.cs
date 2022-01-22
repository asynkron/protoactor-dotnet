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
        private readonly GossipFoo _foo;

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
            _foo = new GossipFoo(myId, getBlockedMembers, instanceLogger, gossipFanout);
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
            _foo.OnClusterTopology(clusterTopology);

        private Task OnAddConsensusCheck(AddConsensusCheck msg) =>
            _foo.OnAddConsensusCheck(msg);

        private Task OnRemoveConsensusCheck(RemoveConsensusCheck request) =>
            _foo.OnRemoveConsensusCheck(request);

        private Task OnGetGossipStateKey(IContext context, GetGossipStateRequest getState)
        {
            var res = _foo.OnGetGossipStateKey(getState);
            context.Respond(res);
            return Task.CompletedTask;
        }

        private Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            var logger = context.Logger()?.BeginScope<GossipActor>();
            logger?.LogDebug("Gossip Request {Sender}", context.Sender!);
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
            ReceiveState(context, gossipRequest.State);
            var senderMember = _foo.TryGetSenderMember(context.Sender!.Address);

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

        private void ReceiveState(IContext context, GossipState remoteState)
        {
            var updates = _foo.ReceiveState(remoteState);

            foreach (var update in updates)
            {
                context.System.EventStream.Publish(update);
            }
        }

        private async Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var res= await _foo.OnSetGossipStateKey(setStateKey);
            context.Respond(res);
        }

        private async Task OnSendGossipState(IContext context)
        {
            var res = await _foo.OnSendGossipState((m, l) => SendGossipForMember(context, m, l));
            context.Respond(res);
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
            _foo.TryGetStateForMember(member, out pendingOffsets, out stateForMember);

        private async Task GossipReenterAfterSend(IContext context, Task<MessageEnvelope> task, ImmutableDictionary<string, long> pendingOffsets)
        {
            var logger = context.Logger();

            try
            {
                await task;
                var envelope = task.Result;

                if (envelope.Message is GossipResponse response)
                {
                    _foo.CommitPendingOffsets(pendingOffsets);

                    if (response.State is not null)
                    {
                        ReceiveState(context, response.State!);

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
                _foo.CommitPendingOffsets(pendingOffsets);
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