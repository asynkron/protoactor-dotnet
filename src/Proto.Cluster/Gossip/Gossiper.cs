// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Remote;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace Proto.Cluster.Gossip;

public sealed record GossiperOptions(
    IRootContext Context,
    IMemberList MemberList,
    BlockList BlockList,
    EventStream EventStream,
    string SystemId,
    Task JoinedCluster,
    CancellationToken Shutdown,
    Func<ActorStatistics> GetActorStatistics,
    int GossipFanout,
    int GossipMaxSend,
    TimeSpan GossipInterval,
    TimeSpan GossipRequestTimeout,
    bool GossipDebugLogging,
    TimeSpan HeartbeatExpiration,
    Func<Task> HeartbeatExpirationHandler);

[PublicAPI]
public partial class Gossiper
{
    public const string GossipActorName = "$gossip";

#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<Gossiper>();
#pragma warning restore CS0618 // Type or member is obsolete
    private readonly IRootContext _context;
    private readonly IMemberList _memberList;
    private readonly BlockList _blockList;
    private readonly EventStream _eventStream;
    private readonly string _systemId;
    private readonly Task _joinedCluster;
    private readonly CancellationToken _shutdown;
    private readonly Func<ActorStatistics> _getActorStatistics;
    private readonly GossiperOptions _options;
    private IGossip _gossip = null!;
    private PID _pid = null!;

    public Gossiper(GossiperOptions options)
    {
        _options = options;
        _context = options.Context;
        _memberList = options.MemberList;
        _blockList = options.BlockList;
        _eventStream = options.EventStream;
        _systemId = options.SystemId;
        _joinedCluster = options.JoinedCluster;
        _shutdown = options.Shutdown;
        _getActorStatistics = options.GetActorStatistics;
    }
}
