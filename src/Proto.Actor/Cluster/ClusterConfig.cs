// -----------------------------------------------------------------------
// <copyright file="ClusterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using JetBrains.Annotations;
using Proto.Cluster.Identity;
using Proto.Cluster.PubSub;
using Proto.Remote;

namespace Proto.Cluster;

[PublicAPI]
public record ClusterConfig
{
    /// <summary>
    ///     Creates new instance of <see cref="ClusterConfig" />.
    /// </summary>
    /// <param name="clusterName">
    ///     A name of the cluster. Various clustering providers will use this name
    ///     to distinguish between different clusters. The value should be the same for all members of the cluster.
    /// </param>
    /// <param name="clusterProvider"><see cref="IClusterProvider" /> to use for the cluster</param>
    /// <param name="identityLookup"><see cref="IdentityLookup" /> implementation to use for the cluster</param>
    /// <exception cref="ArgumentNullException"></exception>
    private ClusterConfig(string clusterName, IClusterProvider clusterProvider, IIdentityLookup identityLookup)
    {
        ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
        ClusterProvider = clusterProvider ?? throw new ArgumentNullException(nameof(clusterProvider));
        ActorRequestTimeout = TimeSpan.FromSeconds(5);
        ActorSpawnVerificationTimeout = TimeSpan.FromSeconds(5);
        ActorActivationTimeout = TimeSpan.FromSeconds(5);
        MaxNumberOfEventsInRequestLogThrottlePeriod = 3;
        RequestLogThrottlePeriod = TimeSpan.FromSeconds(2);
        GossipInterval = TimeSpan.FromMilliseconds(300);
        GossipRequestTimeout = TimeSpan.FromMilliseconds(1500);
        GossipFanout = 3;
        GossipMaxSend = 50;
        HeartbeatExpiration = TimeSpan.FromSeconds(20);
        ClusterRequestDeDuplicationWindow = TimeSpan.FromSeconds(30);
        IdentityLookup = identityLookup;
        MemberStrategyBuilder = (_, _) => new SimpleMemberStrategy();
        RemotePidCacheTimeToLive = TimeSpan.FromMinutes(15);
        RemotePidCacheClearInterval = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    ///     A delegate that returns a <see cref="IMemberStrategy" /> for the given cluster kind.
    ///     By default, <see cref="SimpleMemberStrategy" /> is used for all cluster kinds.
    /// </summary>
    [JsonIgnore]
    public Func<Cluster, string, IMemberStrategy> MemberStrategyBuilder { get; init; }

    /// <summary>
    ///     A name of the cluster. Various clustering providers will use this name
    ///     to distinguish between different clusters. The value should be the same for all members of the cluster.
    /// </summary>
    public string ClusterName { get; }

    /// <summary>
    ///     Cluster kinds define types of the virtual actors supported by this member.
    /// </summary>
    public ImmutableList<ClusterKind> ClusterKinds { get; init; } = ImmutableList<ClusterKind>.Empty;

    /// <summary>
    ///     <see cref="IClusterProvider" /> to use for the cluster.
    /// </summary>
    [JsonIgnore]
    public IClusterProvider ClusterProvider { get; }

    /// <summary>
    ///     Interval between gossip updates. Default is 300ms.
    /// </summary>
    public TimeSpan GossipInterval { get; init; }

    /// <summary>
    ///     The timeout for sending the gossip state to another member. Default is 1500ms.
    /// </summary>
    public TimeSpan GossipRequestTimeout { get; init; }

    /// <summary>
    ///     Gossip heartbeat timeout. If the member does not update its heartbeat within this period, it will be added to the
    ///     <see cref="BlockList" />.
    ///     Default is 20s. Set to <see cref="TimeSpan.Zero" /> to disable.
    /// </summary>
    public TimeSpan HeartbeatExpiration { get; set; }

    /// <summary>
    ///     Timeout for single retry of actor request. Default is 5s.
    ///     Overall timeout for the request is controlled by the cancellation token on
    ///     <see cref="IClusterContext.RequestAsync{T}(ClusterIdentity, object, ISenderContext, CancellationToken)" />
    /// </summary>
    public TimeSpan ActorRequestTimeout { get; init; }

    /// <summary>
    ///     Timeout for running the <see cref="ClusterKind.CanSpawnIdentity" /> check. Default is 5s.
    /// </summary>
    public TimeSpan ActorSpawnVerificationTimeout { get; init; }

    /// <summary>
    ///     Timeout for activating an actor. Exact usage varies depending on <see cref="IIdentityLookup" /> used. Default is
    ///     5s.
    /// </summary>
    public TimeSpan ActorActivationTimeout { get; init; }

    /// <summary>
    ///     Throttle maximum events logged from cluster requests in a period of time. Specify period in this property. The
    ///     default is 2s.
    /// </summary>
    public TimeSpan RequestLogThrottlePeriod { get; init; }

    /// <summary>
    ///     Throttle maximum events logged from cluster requests in a period of time. Specify number of events in this
    ///     property. The default is 3.
    /// </summary>
    public int MaxNumberOfEventsInRequestLogThrottlePeriod { get; init; }

    /// <summary>
    ///     The number of random members the gossip will be sent to during each gossip update. Default is 3.
    /// </summary>
    public int GossipFanout { get; init; }

    /// <summary>
    ///     Maximum number of member states to be sent to each member receiving gossip. Default is 50.
    /// </summary>
    public int GossipMaxSend { get; init; }

    /// <summary>
    ///     The <see cref="IIdentityLookup" /> to use for the cluster
    /// </summary>
    [JsonIgnore]
    public IIdentityLookup IdentityLookup { get; }

    /// <summary>
    ///     Default window size for cluster deduplication (<see cref="Extensions.WithClusterRequestDeduplication" />). Default
    ///     is 30s.
    /// </summary>
    public TimeSpan ClusterRequestDeDuplicationWindow { get; init; }

    /// <summary>
    ///     TTL for remote PID cache. Default is 15min. Set to <see cref="TimeSpan.Zero" /> to disable.
    /// </summary>
    public TimeSpan RemotePidCacheTimeToLive { get; set; }

    /// <summary>
    ///     How often to check for stale PIDs in the remote PID cache. Default is 15s. Set to <see cref="TimeSpan.Zero" /> to
    ///     disable.
    /// </summary>
    public TimeSpan RemotePidCacheClearInterval { get; set; }

    /// <summary>
    ///     Creates the <see cref="IClusterContext" />. The default implementation creates an instance of
    ///     <see cref="DefaultClusterContext" />
    /// </summary>
    [JsonIgnore]
    public Func<Cluster, IClusterContext> ClusterContextProducer { get; init; } = c => new DefaultClusterContext(c);

    /// <summary>
    ///     Configuration for the PubSub extension.
    /// </summary>
    public PubSubConfig PubSubConfig { get; init; } = PubSubConfig.Setup();

    /// <summary>
    ///     Backwards compatibility. Set to true to have timed out requests return default(TResponse) instead of throwing
    ///     <see cref="TimeoutException" />
    ///     Default is false.
    /// </summary>
    public bool LegacyRequestTimeoutBehavior { get; init; }

    /// <summary>
    ///     Exit the application process when the cluster is terminated. Default is false.
    /// </summary>
    public bool ExitOnShutdown { get; set; } = false;

    /// <summary>
    ///     Timeout for single retry of actor request. Default is 5s.
    ///     Overall timeout for the request is controlled by the cancellation token on
    ///     <see cref="IClusterContext.RequestAsync{T}(ClusterIdentity, object, ISenderContext, CancellationToken)" />
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public ClusterConfig WithActorRequestTimeout(TimeSpan timeSpan) => this with { ActorRequestTimeout = timeSpan };

    /// <summary>
    ///     Timeout for running the <see cref="ClusterKind.CanSpawnIdentity" /> check. Default is 5s.
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public ClusterConfig WithActorSpawnVerificationTimeout(TimeSpan timeSpan) =>
        this with { ActorSpawnVerificationTimeout = timeSpan };

    /// <summary>
    ///     Timeout for DB Identity Lookup operations. Default is 5s.
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public ClusterConfig WithActorActivationTimeout(TimeSpan timeSpan) =>
        this with { ActorActivationTimeout = timeSpan };

    /// <summary>
    ///     Throttle maximum events logged from cluster requests in a period of time. Specify period in this property. The
    ///     default is 2s.
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public ClusterConfig WithRequestLogThrottlePeriod(TimeSpan timeSpan) =>
        this with { RequestLogThrottlePeriod = timeSpan };

    /// <summary>
    ///     Throttle maximum events logged from cluster requests in a period of time. Specify number of events in this
    ///     property. The default is 3.
    /// </summary>
    /// <param name="max"></param>
    /// <returns></returns>
    public ClusterConfig WithMaxNumberOfEventsInRequestLogThrottlePeriod(int max) =>
        this with { MaxNumberOfEventsInRequestLogThrottlePeriod = max };

    /// <summary>
    ///     Adds a <see cref="ClusterKind" /> to this member
    /// </summary>
    /// <param name="kind">Kind name</param>
    /// <param name="prop">Props to spawn an actor of this kind</param>
    /// <returns></returns>
    public ClusterConfig WithClusterKind(string kind, Props prop) => WithClusterKind(new ClusterKind(kind, prop));

    /// <summary>
    ///     Adds a <see cref="ClusterKind" /> to this member
    /// </summary>
    /// <param name="kind">Kind name</param>
    /// <param name="prop">Props to spawn an actor of this kind</param>
    /// <param name="strategyBuilder">Specifies <see cref="IMemberStrategy" /> for this cluster kind</param>
    /// <returns></returns>
    public ClusterConfig WithClusterKind(string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder) =>
        WithClusterKind(new ClusterKind(kind, prop) { StrategyBuilder = strategyBuilder });

    /// <summary>
    ///     Adds <see cref="ClusterKind" /> list to this member
    /// </summary>
    /// <param name="knownKinds">List of tuples of (kind name, props to spawn an actor of this kind)</param>
    /// <returns></returns>
    public ClusterConfig WithClusterKinds(params (string kind, Props prop)[] knownKinds) =>
        WithClusterKinds(knownKinds.Select(k => new ClusterKind(k.kind, k.prop)).ToArray());

    /// <summary>
    ///     Adds <see cref="ClusterKind" /> list to this member
    /// </summary>
    /// <param name="knownKinds">
    ///     List of tuples of (kind name, props to spawn an actor of this kind,
    ///     <see cref="IMemberStrategy" /> for this kind)
    /// </param>
    /// <returns></returns>
    public ClusterConfig WithClusterKinds(
        params (string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder)[] knownKinds) =>
        WithClusterKinds(knownKinds
            .Select(k => new ClusterKind(k.kind, k.prop) { StrategyBuilder = k.strategyBuilder })
            .ToArray());

    /// <summary>
    ///     Adds a <see cref="ClusterKind" /> to this member
    /// </summary>
    /// <param name="clusterKind"></param>
    /// <returns></returns>
    public ClusterConfig WithClusterKind(ClusterKind clusterKind) => WithClusterKinds(clusterKind);

    /// <summary>
    ///     Adds <see cref="ClusterKind" /> list to this member
    /// </summary>
    /// <param name="clusterKinds"></param>
    /// <returns></returns>
    public ClusterConfig WithClusterKinds(params ClusterKind[] clusterKinds) =>
        this with { ClusterKinds = ClusterKinds.AddRange(clusterKinds) };

    /// <summary>
    ///     Sets the default <see cref="IMemberStrategy" /> for this cluster kinds
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public ClusterConfig WithMemberStrategyBuilder(Func<Cluster, string, IMemberStrategy> builder) =>
        this with { MemberStrategyBuilder = builder };

    /// <summary>
    ///     Sets the delegate that creates the <see cref="IClusterContext" />. The default implementation creates an instance
    ///     of <see cref="DefaultClusterContext" />
    /// </summary>
    /// <param name="producer"></param>
    /// <returns></returns>
    public ClusterConfig WithClusterContextProducer(Func<Cluster, IClusterContext> producer) =>
        this with { ClusterContextProducer = producer };

    /// <summary>
    ///     Interval between gossip updates. Default is 300ms.
    /// </summary>
    /// <param name="interval"></param>
    /// <returns></returns>
    public ClusterConfig WithGossipInterval(TimeSpan interval) => this with { GossipInterval = interval };

    /// <summary>
    ///     The number of random members the gossip will be sent to during each gossip update. Default is 3.
    /// </summary>
    /// <param name="fanout"></param>
    /// <returns></returns>
    public ClusterConfig WithGossipFanOut(int fanout) => this with { GossipFanout = fanout };

    /// <summary>
    ///     Maximum number of member states to be sent to each member receiving gossip. Default is 50.
    /// </summary>
    /// <param name="maxSend"></param>
    /// <returns></returns>
    public ClusterConfig WithGossipMaxSend(int maxSend) => this with { GossipMaxSend = maxSend };

    /// <summary>
    ///     The timeout for sending the gossip state to another member. Default is 1500ms.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public ClusterConfig WithGossipRequestTimeout(TimeSpan timeout) => this with { GossipRequestTimeout = timeout };

    /// <summary>
    ///     TTL for remote PID cache. Default is 15min. Set to <see cref="TimeSpan.Zero" /> to disable.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public ClusterConfig WithRemotePidCacheTimeToLive(TimeSpan timeout) =>
        this with { RemotePidCacheTimeToLive = timeout };

    /// <summary>
    ///     Gossip heartbeat timeout. If the member does not update its heartbeat within this period, it will be added to the
    ///     <see cref="BlockList" />.
    ///     Default is 20s. Set to <see cref="TimeSpan.Zero" /> to disable.
    /// </summary>
    /// <param name="expiration"></param>
    /// <returns></returns>
    public ClusterConfig WithHeartbeatExpiration(TimeSpan expiration) => this with { HeartbeatExpiration = expiration };

    /// <summary>
    ///     Configuration for the PubSub extension.
    /// </summary>
    public ClusterConfig WithPubSubConfig(PubSubConfig config) => this with { PubSubConfig = config };

    /// <summary>
    ///     Backwards compatibility. Set to true to have timed out requests return default(TResponse) instead of throwing
    ///     <see cref="TimeoutException" />
    ///     Default is false.
    /// </summary>
    public ClusterConfig WithLegacyRequestTimeoutBehavior(bool enabled = true) =>
        this with { LegacyRequestTimeoutBehavior = enabled };
    
    
    /// <summary>
    ///     Exit the application process when the cluster is shutdown.
    /// </summary>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public ClusterConfig WithExitOnShutdown(bool enabled = true) => 
        this with { ExitOnShutdown = enabled };

    /// <summary>
    ///     Creates a new <see cref="ClusterConfig" />
    /// </summary>
    /// <param name="clusterName">
    ///     A name of the cluster. Various clustering providers will use this name
    ///     to distinguish between different clusters. The value should be the same for all members of the cluster.
    /// </param>
    /// <param name="clusterProvider"><see cref="IClusterProvider" /> to use for the cluster</param>
    /// <param name="identityLookup"><see cref="IdentityLookup" /> implementation to use for the cluster</param>
    /// <returns></returns>
    public static ClusterConfig Setup(
        string clusterName,
        IClusterProvider clusterProvider,
        IIdentityLookup identityLookup
    ) =>
        new(clusterName, clusterProvider, identityLookup);
}