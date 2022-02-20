// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Context;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
        private readonly TimeSpan _gossipRequestTimeout;
        private readonly IGossip _internal;

        // lookup from state key -> consensus checks

        public GossipActor(
            TimeSpan gossipRequestTimeout,
            string myId,
            Func<ImmutableHashSet<string>> getBlockedMembers,
            InstanceLogger? instanceLogger,
            int gossipFanout,
            int gossipMaxSend
        )
        {
            _gossipRequestTimeout = gossipRequestTimeout;
            _internal = new Gossip(myId, gossipFanout, gossipMaxSend, getBlockedMembers, instanceLogger);
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState         => OnSetGossipStateKey(context, setState),
            GetGossipStateRequest getState     => OnGetGossipStateKey(context, getState),
            GetGossipStateEntryRequest getState     => OnGetGossipStateEntryKey(context, getState),
            GetGossipStateSnapshot getSnapshot => OnGetGossipStateSnapshot(context),
            GossipRequest gossipRequest        => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest             => OnSendGossipState(context),
            AddConsensusCheck request          => OnAddConsensusCheck(context, request),
            ClusterTopology clusterTopology    => OnClusterTopology(clusterTopology),
            _                                  => Task.CompletedTask
        };

        private Task OnGetGossipStateEntryKey(IContext context, GetGossipStateEntryRequest getState)
        {
            var state = _internal.GetStateEntry(getState.Key);
            var res = new GetGossipStateEntryResponse(state);
            context.Respond(res);
            return Task.CompletedTask;
        }

        private Task OnGetGossipStateSnapshot(IContext context)
        {
            var state = _internal.GetStateSnapshot();
            context.Respond(state);
            return Task.CompletedTask;
        }

        private Task OnClusterTopology(ClusterTopology clusterTopology) =>
            _internal.UpdateClusterTopology(clusterTopology);

        private Task OnAddConsensusCheck(IContext context, AddConsensusCheck msg)
        {
            var id = Guid.NewGuid().ToString();
            _internal.AddConsensusCheck(id, msg.Check);
            context.ReenterAfterCancellation(msg.Token, () => _internal.RemoveConsensusCheck(id));

            return Task.CompletedTask;
        }

        private Task OnGetGossipStateKey(IContext context, GetGossipStateRequest getState)
        {
            var state = _internal.GetState(getState.Key);
            var res = new GetGossipStateResponse(state);
            context.Respond(res);
            return Task.CompletedTask;
        }

        private Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            var logger = context.Logger()?.BeginScope<GossipActor>();
            logger?.LogDebug("Gossip Request {Sender}", context.Sender!);
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
            ReceiveState(context, gossipRequest.State);

            //it's OK, we might not just yet be aware of this member yet....
            
            // if (!context.Cluster().MemberList.ContainsMemberId(gossipRequest.MemberId))
            // {
            //     Logger.LogWarning("Got gossip request from unknown member {MemberId}", gossipRequest.MemberId);
            //
            //     // Nothing to send, do not provide sender or state payload
            //     context.Respond(new GossipResponse());
            //     return Task.CompletedTask;
            // }

            var memberState = _internal.GetMemberStateDelta(gossipRequest.MemberId);

            if (!memberState.HasState)
            {
                Logger.LogDebug("Got gossip request from member {MemberId}, but no state was found", gossipRequest.MemberId);

                // Nothing to send, do not provide sender or state payload
                context.Respond(new GossipResponse());
                return Task.CompletedTask;
            }

            context.RequestReenter<GossipResponseAck>(context.Sender!, new GossipResponse
                {
                    State = memberState.State
                }, task => ReenterAfterResponseAck(context, task, memberState), context.CancellationToken
            );

            return Task.CompletedTask;
        }

        private void ReceiveState(IContext context, GossipState remoteState)
        {
            var updates = _internal.ReceiveState(remoteState);

            foreach (var update in updates)
            {
                context.System.EventStream.Publish(update);
            }
        }

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var (key, message) = setStateKey;
            _internal.SetState(key, message);

            if (context.Sender != null)
            {
                context.Respond(new SetGossipStateResponse());
            }

            return Task.CompletedTask;
        }

        private Task OnSendGossipState(IContext context)
        {
            _internal.SendState((memberState, member, logger) => SendGossipForMember(context, member, logger, memberState));
            context.Respond(new SendGossipStateResponse());
            return Task.CompletedTask;
        }

        private void SendGossipForMember(IContext context, Member member, InstanceLogger? logger, MemberStateDelta memberStateDelta)
        {
            var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);

            logger?.LogInformation("Sending GossipRequest to {MemberId}", member.Id);
            Logger.LogDebug("Sending GossipRequest to {MemberId}", member.Id);

            //a short timeout is massively important, we cannot afford hanging around waiting for timeout, blocking other gossips from getting through

            // This will return a GossipResponse, but since we need could need to get the sender, we do not unpack it from the MessageEnvelope
            context.RequestReenter<MessageEnvelope>(pid, new GossipRequest
                {
                    MemberId = context.System.Id,
                    State = memberStateDelta.State
                },
                task => GossipReenterAfterSend(context, task, memberStateDelta),
                CancellationTokens.WithTimeout(_gossipRequestTimeout)
            );
        }

        private async Task GossipReenterAfterSend(IContext context, Task<MessageEnvelope> task, MemberStateDelta delta)
        {
            var logger = context.Logger();

            try
            {
                await task;
                var envelope = task.Result;

                if (envelope.Message is GossipResponse response)
                {
                    delta.CommitOffsets();

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
                Logger.LogWarning("DeadLetter in GossipReenterAfterSend");
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning("Timeout");
                Logger.LogWarning("Timeout in GossipReenterAfterSend");
            }
            catch (TimeoutException)
            {
                logger?.LogWarning("Timeout");
                Logger.LogWarning("Timeout in GossipReenterAfterSend");
            }
            catch (Exception x)
            {
                logger?.LogError(x, "OnSendGossipState failed");
                Logger.LogError(x, "OnSendGossipState failed");
            }
        }

        private async Task ReenterAfterResponseAck(IContext context, Task<GossipResponseAck> task, MemberStateDelta delta)
        {
            var logger = context.Logger();

            try
            {
                await task;
                delta.CommitOffsets();
            }
            catch (DeadLetterException)
            {
                logger?.LogWarning("DeadLetter");
                Logger.LogWarning("DeadLetter in GossipReenterAfterSend");
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning("Timeout");
                Logger.LogWarning("Timeout in GossipReenterAfterSend");
            }
            catch (TimeoutException)
            {
                logger?.LogWarning("Timeout");
                Logger.LogWarning("Timeout in GossipReenterAfterSend");
            }
            catch (Exception x)
            {
                logger?.LogError(x, "OnSendGossipState failed");
                Logger.LogError(x, "OnSendGossipState failed");
            }
        }
    }
}