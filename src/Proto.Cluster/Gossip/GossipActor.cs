// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using Proto.Logging;
using Proto.Remote;

namespace Proto.Cluster.Gossip;

public class GossipActor : IActor
{
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
#pragma warning restore CS0618 // Type or member is obsolete
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
            () => system.Cluster().MemberList.GetMembers(), system.Cluster().Config.GossipDebugLogging);
    }

    public async Task ReceiveAsync(IContext context)
    {
        try
        {
         //   Logger.LogInformation("GossipActor Received {MessageType}", context.Message.GetMessageTypeName());
            var t = context.Message switch
            {
                SetGossipStateKey setState => OnSetGossipStateKey(context, setState),
                GetGossipStateRequest getState => OnGetGossipStateKey(context, getState),
                GetGossipStateEntryRequest getState => OnGetGossipStateEntryKey(context, getState),
                GetGossipStateSnapshot => OnGetGossipStateSnapshot(context),
                GossipRequest gossipRequest => OnGossipRequest(context, gossipRequest),
                SendGossipStateRequest => OnSendGossipState(context),
                AddConsensusCheck request => OnAddConsensusCheck(context, request),
                ClusterTopology clusterTopology => OnClusterTopology(clusterTopology),
                _ => Task.CompletedTask
            };
            await t;
         //   Logger.LogInformation("GossipActor Done {MessageType}", context.Message.GetMessageTypeName());
        }
        catch (Exception x)
        {
            Logger.LogError(x, "GossipActor Failed {MessageType}", context.Message.GetMessageTypeName());
        }
    }

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
        
        if (context.Remote().BlockList.BlockedMembers.Contains(gossipRequest.MemberId))
        {
            Logger.LogInformation("Blocked gossip request from {MemberId}", gossipRequest.MemberId);
            context.Respond(new GossipResponse()
            {
                Rejected = true,
            });
            return Task.CompletedTask;
        }

        if (!context.Cluster().MemberList.ContainsMemberId(gossipRequest.MemberId))
        {
            Logger.LogInformation("Ignoring gossip request from {MemberId} as it is not a member", gossipRequest.MemberId);
            context.Respond(new GossipResponse()
            {
                Rejected = true,
            });
            return Task.CompletedTask;
        }
        
        
        

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
        }
        
        ReceiveState(context, gossipRequest.State);

        
        if (context.Cluster().Config.GossipDebugLogging)
        {
            Logger.LogInformation("Responding to GossipRequest {Request} to {MemberId}", gossipRequest, gossipRequest.MemberId);
        }
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
        _internal.SendState((memberState, member, logger) => SendGossipForMember(context, member, memberState));
        context.Respond(new SendGossipStateResponse());

        return Task.CompletedTask;
    }

    private void SendGossipForMember(IContext context, Member targetMember,
        MemberStateDelta memberStateDelta)
    {
        var pid = PID.FromAddress(targetMember.Address, Gossiper.GossipActorName);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Sending GossipRequest to {MemberId}", targetMember.Id);
        }

        var start = DateTime.UtcNow;
        var gossipRequest = new GossipRequest
        {
            MemberId = context.System.Id,
            State = memberStateDelta.State.Clone() //ensure we have a copy and not send state that might mutate
        };
        if (context.Cluster().Config.GossipDebugLogging)
        {
            Logger.LogInformation("Sending GossipRequest {Request} to {MemberId}", gossipRequest, targetMember.Id);
        }
        context.RequestReenter<GossipResponse>(pid, gossipRequest,
            async task =>
            {
                var delta = DateTime.UtcNow - start;
                var self = context.Cluster().MemberList.Self;
                
                //if the target is no longer part of the cluster. don't log. the failure is expected.. issue #1992
                if (!context.Cluster().MemberList.TryGetMember(targetMember.Id, out _))
                {
                    return;
                }

                try
                {
                    var res = await task.ConfigureAwait(false);
                    if (res.Rejected)
                    {
                        //we could be smarter here. rejected because of block? then init shutdown
                        return;
                    }
                    
                    memberStateDelta.CommitOffsets();
                }
                catch (TimeoutException)
                {
                    //log member issue #1993
                    Logger.LogWarning(
                        "Timeout in GossipReenterAfterSend, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                        delta.TotalMilliseconds, targetMember, self);
                }
                catch (DeadLetterException x)
                {
                    Logger.LogDebug(x,
                        "DeadLetter in GossipReenterAfterSend, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                        delta.TotalMilliseconds, targetMember, self);
                }
                catch (Exception x)
                {
                    //if the target is no longer part of the cluster. don't log. the failure is expected.. issue #1992
                    if (context.Cluster().MemberList.TryGetMember(targetMember.Id, out _))
                    {
                        //log member issue #1993
                        Logger.LogError(x,
                            "GossipReenterAfterSend failed, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                            delta.TotalMilliseconds, targetMember, self);
                    }
                }
            },
            CancellationTokens.WithTimeout(_gossipRequestTimeout)
        );
    }
}