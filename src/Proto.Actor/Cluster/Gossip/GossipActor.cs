// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

public class GossipActor : IActor
{
    private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
    private readonly TimeSpan _gossipRequestTimeout;
    private readonly IGossip _internal;

    // lookup from state key -> consensus checks

    public GossipActor(
        ActorSystem system,
        TimeSpan gossipRequestTimeout,
        InstanceLogger? instanceLogger,
        int gossipFanout,
        int gossipMaxSend
    )
    {
        _gossipRequestTimeout = gossipRequestTimeout;
        _internal = new Gossip(system.Id, gossipFanout, gossipMaxSend, instanceLogger,
            () => system.Cluster().MemberList.GetMembers());
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            SetGossipStateKey setState          => OnSetGossipStateKey(context, setState),
            GetGossipStateRequest getState      => OnGetGossipStateKey(context, getState),
            GetGossipStateEntryRequest getState => OnGetGossipStateEntryKey(context, getState),
            GetGossipStateSnapshot              => OnGetGossipStateSnapshot(context),
            GossipRequest gossipRequest         => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest              => OnSendGossipState(context),
            AddConsensusCheck request           => OnAddConsensusCheck(context, request),
            ClusterTopology clusterTopology     => OnClusterTopology(clusterTopology),
            _                                   => Task.CompletedTask
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

    private Task OnClusterTopology(ClusterTopology clusterTopology) => _internal.UpdateClusterTopology(clusterTopology);

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

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
        }

        ReceiveState(context, gossipRequest.State);
        
        context.Respond(new GossipResponse());

        
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

    private void SendGossipForMember(IContext context, Member member, InstanceLogger? logger,
        MemberStateDelta memberStateDelta)
    {
        var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Sending GossipRequest to {MemberId}", member.Id);
        }

        var start = DateTime.UtcNow;
        context.RequestReenter<GossipResponse>(pid, new GossipRequest
            {
                MemberId = context.System.Id,
                State = memberStateDelta.State.Clone() //ensure we have a copy and not send state that might mutate
            },
            async task =>
            {
                var delta = DateTime.UtcNow - start;
                try
                {
                    await task.ConfigureAwait(false);
                    memberStateDelta.CommitOffsets();
                }
                catch (DeadLetterException)
                {
                    Logger.LogWarning("DeadLetter in GossipReenterAfterSend, elapsed {Delta}ms",delta.TotalMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    Logger.LogWarning("OperationCancel in GossipReenterAfterSend, elapsed {Delta}ms",delta.TotalMilliseconds);
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning("Timeout in GossipReenterAfterSend, elapsed {Delta}ms",delta.TotalMilliseconds);
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "GossipReenterAfterSend failed, elapsed {Delta}ms",delta.TotalMilliseconds);
                }
            },
            CancellationTokens.WithTimeout(_gossipRequestTimeout)
        );
    }
}