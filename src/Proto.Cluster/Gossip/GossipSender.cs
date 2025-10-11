// -----------------------------------------------------------------------
// <copyright file="GossipSender.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Logging;
using Proto.Cluster;

namespace Proto.Cluster.Gossip;

internal static class GossipSender
{
    private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();

    public static void Send(
        IContext context,
        IMemberList memberList,
        Member targetMember,
        MemberStateDelta memberStateDelta,
        GossipRequest request,
        TimeSpan timeout,
        IGossipTransport transport)
    {
        var pid = PID.FromAddress(targetMember.Address, Gossiper.GossipActorName);
        var start = DateTime.UtcNow;

        transport.Request(
            context,
            pid,
            request,
            async task =>
            {
                var delta = DateTime.UtcNow - start;
                var self = memberList.Self;

                if (!memberList.TryGetMember(targetMember.Id, out _))
                {
                    return;
                }

                try
                {
                    var res = await task.ConfigureAwait(false);
                    if (res.Rejected)
                    {
                        return;
                    }

                    memberStateDelta.CommitOffsets();
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning(
                        "Timeout in GossipReenterAfterSend, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                        delta.TotalMilliseconds,
                        targetMember,
                        self);
                }
                catch (DeadLetterException x)
                {
                    Logger.LogDebug(
                        x,
                        "DeadLetter in GossipReenterAfterSend, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                        delta.TotalMilliseconds,
                        targetMember,
                        self);
                }
                catch (Exception x)
                {
                    if (memberList.TryGetMember(targetMember.Id, out _))
                    {
                        Logger.LogError(
                            x,
                            "GossipReenterAfterSend failed, elapsed {Delta}ms for target member {TargetMember} from {SelfMember}",
                            delta.TotalMilliseconds,
                            targetMember,
                            self);
                    }
                }
            },
            CancellationTokens.WithTimeout(timeout));
    }
}
