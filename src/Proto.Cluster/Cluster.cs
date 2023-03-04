// -----------------------------------------------------------------------
// <copyright file="Cluster.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;
using Proto.Cluster.Identity;
using Proto.Cluster.Metrics;
using Proto.Cluster.PubSub;
using Proto.Cluster.Seed;
using Proto.Diagnostics;
using Proto.Extensions;
using Proto.Remote;
using Proto.Utils;

namespace Proto.Cluster;

/// <summary>
///     The cluster extension for <see cref="ActorSystem" />
/// </summary>
[PublicAPI]
public class Cluster : IActorSystemExtension<Cluster>
{
    private Func<IEnumerable<Measurement<long>>>? _clusterKindObserver;
    private readonly Dictionary<string, ActivatedClusterKind> _clusterKinds = new();
    private Func<IEnumerable<Measurement<long>>>? _clusterMembersObserver;
    private readonly TaskCompletionSource<bool> _shutdownCompletedTcs = new();
    private readonly TaskCompletionSource<bool> _joinedClusterTcs = new();

    public async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        var res = new List<DiagnosticsEntry>();

        var now = new DiagnosticsEntry("Cluster", "Local Time", DateTimeOffset.UtcNow);
        res.Add(now);
        
        var blocked = new DiagnosticsEntry("Cluster", "Blocked", System.Remote().BlockList.BlockedMembers.ToArray());
        res.Add(blocked);
        
        var t = await Gossip.GetState<ClusterTopology>(GossipKeys.Topology).ConfigureAwait(false);

        var topology = new DiagnosticsEntry("Cluster", "Topology", t);
        res.Add(topology);
        
        var h = await Gossip.GetStateEntry(GossipKeys.Heartbeat).ConfigureAwait(false);
        var heartbeats = h.Select(heartbeat => new DiagnosticsMemberHeartbeat(heartbeat.Key, heartbeat.Value.Value.Unpack<MemberHeartbeat>(), heartbeat.Value.LocalTimestamp)).ToArray();
        
        var heartbeat = new DiagnosticsEntry("Cluster", "Heartbeat", heartbeats);
        res.Add(heartbeat);

        var idlookup = await IdentityLookup.GetDiagnostics().ConfigureAwait(false);
        res.AddRange(idlookup);

        var provider = await Provider.GetDiagnostics().ConfigureAwait(false);
        res.AddRange(provider);

        return res.ToArray();
    }

    public Cluster(ActorSystem system, ClusterConfig config)
    {
        System = system;
        Config = config;

        system.Extensions.Register(this);

        //register cluster messages
        var serialization = system.Serialization();
        serialization.RegisterFileDescriptor(ClusterContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(GossipContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(PubSubContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(GrainContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(SeedContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(EmptyReflection.Descriptor);

        Gossip = new Gossiper(this);
        PidCache = new PidCache();
        _ = new PubSubExtension(this);

        if (System.Metrics.Enabled)
        {
            _clusterMembersObserver = () => new[]
            {
                new Measurement<long>(MemberList.GetAllMembers().Length,
                    new KeyValuePair<string, object?>("id", System.Id),
                    new KeyValuePair<string, object?>("address", System.Address))
            };

            ClusterMetrics.ClusterMembersCount.AddObserver(_clusterMembersObserver);
        }

        SubscribeToTopologyEvents();
    }

    internal static ILogger Logger { get; } = Log.CreateLogger<Cluster>();

    internal IClusterContext ClusterContext { get; private set; } = null!;

    public Gossiper Gossip { get; }

    /// <summary>
    ///     Cluster config used by this cluster
    /// </summary>
    public ClusterConfig Config { get; }

    /// <summary>
    ///     Actor system this cluster is running on
    /// </summary>
    public ActorSystem System { get; }

    /// <summary>
    ///     IRemote implementation the cluster is using
    /// </summary>
    public IRemote Remote { get; private set; } = null!;

    /// <summary>
    ///     Awaitable task which will complete when this cluster has joined the cluster
    /// </summary>
    public Task JoinedCluster => _joinedClusterTcs.Task;
    /// <summary>
    ///     Awaitable task which will complete when this cluster has completed shutdown
    /// </summary>
    public Task ShutdownCompleted => _shutdownCompletedTcs.Task;

    /// <summary>
    ///     A list of known cluster members. See <see cref="Proto.Cluster.MemberList" /> for details
    /// </summary>
    public MemberList MemberList { get; private set; } = null!;

    internal IIdentityLookup IdentityLookup { get; set; } = null!;

    internal IClusterProvider Provider { get; set; } = null!;

    internal PidCache PidCache { get; }

    private void SubscribeToTopologyEvents() =>
        System.EventStream.Subscribe<ClusterTopology>(e =>
            {
                foreach (var member in e.Left)
                {
                    PidCache.RemoveByMember(member);
                }
            }
        );

    /// <summary>
    ///     Gets cluster kinds registered on this cluster member
    /// </summary>
    /// <returns></returns>
    public string[] GetClusterKinds() => _clusterKinds.Keys.ToArray();

    /// <summary>
    ///     Starts the cluster member
    /// </summary>
    public async Task StartMemberAsync()
    {
        await BeginStartAsync(false).ConfigureAwait(false);
        //gossiper must be started whenever any topology events starts flowing
        await Gossip.StartAsync().ConfigureAwait(false);
        MemberList.InitializeTopologyConsensus();
        await Provider.StartMemberAsync(this).ConfigureAwait(false);
        Logger.LogInformation("Started as cluster member");
        await MemberList.Started.ConfigureAwait(false);
        Logger.LogInformation("I see myself");
        System.Diagnostics.RegisterEvent("Cluster", "Started Member Successfully");
    }

    /// <summary>
    ///     Start the cluster member in client mode. A client member will not spawn virtual actors, but can talk to other
    ///     members.
    /// </summary>
    public async Task StartClientAsync()
    {
        await BeginStartAsync(true).ConfigureAwait(false);
        await Provider.StartClientAsync(this).ConfigureAwait(false);

        Logger.LogInformation("Started as cluster client");
        System.Diagnostics.RegisterEvent("Cluster", "Started Client Successfully");
    }

    private async Task BeginStartAsync(bool client)
    {
        InitClusterKinds(client);
        Provider = Config.ClusterProvider;
        //default to partition identity lookup
        IdentityLookup = Config.IdentityLookup;

        Remote = System.Extensions.GetRequired<IRemote>("Remote module must be configured when using cluster");
        await Remote.StartAsync().ConfigureAwait(false);

        Logger.LogInformation("Starting");
        MemberList = new MemberList(this);
        _ = MemberList.Started.ContinueWith(_ => _joinedClusterTcs.TrySetResult(true));
        ClusterContext = Config.ClusterContextProducer(this);

        var kinds = GetClusterKinds();
        await IdentityLookup.SetupAsync(this, kinds, client).ConfigureAwait(false);
        InitIdentityProxy();
        await this.PubSub().StartAsync().ConfigureAwait(false);
        InitPidCacheTimeouts();
        System.Diagnostics.RegisterObject("Cluster","Config", Config);
    }

    private void InitPidCacheTimeouts()
    {
        if (Config.RemotePidCacheClearInterval > TimeSpan.Zero && Config.RemotePidCacheTimeToLive > TimeSpan.Zero)
        {
            _ = Task.Run(async () =>
                {
                    while (!System.Shutdown.IsCancellationRequested)
                    {
                        await Task.Delay(Config.RemotePidCacheClearInterval, System.Shutdown).ConfigureAwait(false);
                        PidCache.RemoveIdleRemoteProcessesOlderThan(Config.RemotePidCacheTimeToLive);
                    }
                }, System.Shutdown
            );
        }
    }

    private void InitClusterKinds(bool client)
    {
        foreach (var clusterKind in Config.ClusterKinds)
        {
            _clusterKinds.Add(clusterKind.Name, clusterKind.Build(this));
        }

        if (!client)
        {
            EnsureTopicKindRegistered();
        }

        if (System.Metrics.Enabled)
        {
            _clusterKindObserver = () =>
                _clusterKinds.Values
                    .Select(ck =>
                        new Measurement<long>(ck.Count, new KeyValuePair<string, object?>("id", System.Id),
                            new KeyValuePair<string, object?>("address", System.Address),
                            new KeyValuePair<string, object?>("clusterkind", ck.Name))
                    );

            ClusterMetrics.VirtualActorsCount.AddObserver(_clusterKindObserver);
        }
    }

    private void EnsureTopicKindRegistered()
    {
        // make sure PubSub topic kind is registered if user did not provide a custom registration
        if (!_clusterKinds.ContainsKey(TopicActor.Kind))
        {
            var store = new EmptyKeyValueStore<Subscribers>();

            _clusterKinds.Add(
                TopicActor.Kind,
                new ClusterKind(TopicActor.Kind, Props.FromProducer(() => new TopicActor(store))).Build(this)
            );
        }
    }

    private void InitIdentityProxy() =>
        System.Root.SpawnNamedSystem(Props.FromProducer(() => new IdentityActivatorProxy(this)),
            IdentityActivatorProxy.ActorName);

    /// <summary>
    ///     Shuts down the cluster member, <see cref="Proto.Remote.IRemote" /> extensions and the <see cref="ActorSystem" />
    /// </summary>
    /// <param name="graceful">
    ///     When true, this operation will await the shutdown of virtual actors managed by this member.
    ///     This flag is also used by some of the clustering providers to explicitly deregister the member. When the shutdown
    ///     is ungraceful,
    ///     the member would have to reach its TTL to be removed in those cases.
    /// </param>
    /// <param name="reason">Provide the reason for the shutdown, that can be used for diagnosing problems</param>
    public async Task ShutdownAsync(bool graceful = true, string reason = "")
    {
        Logger.LogInformation("Stopping Cluster {Id}", System.Id);

        // Inform all members of the cluster that this node intends to leave. Also, let the MemberList know that this
        // node was the one that initiated the shutdown to prevent another shutdown from being called.
        MemberList.Stopping = true;
        await Gossip.SetStateAsync(GossipKeys.GracefullyLeft, new Empty()).ConfigureAwait(false);

        // Deregister from configured cluster provider.
        await Provider.ShutdownAsync(graceful).ConfigureAwait(false);

        // In case provider shutdown is quick, let's wait at least 2 gossip intervals.
        await Task.Delay((int)Config.GossipInterval.TotalMilliseconds * 2).ConfigureAwait(false);

        if (_clusterKindObserver != null)
        {
            ClusterMetrics.VirtualActorsCount.RemoveObserver(_clusterKindObserver);
            _clusterKindObserver = null;
        }

        if (_clusterMembersObserver != null)
        {
            ClusterMetrics.ClusterMembersCount.RemoveObserver(_clusterMembersObserver);
            _clusterMembersObserver = null;
        }

        // Cancel the primary CancellationToken first which will shut down a number of concurrent systems simultaneously.
        await System.ShutdownAsync(reason).ConfigureAwait(false);

        // Shut down the rest of the dependencies in reverse order that they were started.
        await Gossip.ShutdownAsync().ConfigureAwait(false);

        if (graceful)
        {
            await IdentityLookup.ShutdownAsync().ConfigureAwait(false);
        }

        await Remote.ShutdownAsync(graceful).ConfigureAwait(false);

        _shutdownCompletedTcs.TrySetResult(true);
        Logger.LogInformation("Stopped Cluster {Id}", System.Id);
    }

    /// <summary>
    ///     Resolves cluster identity to a <see cref="PID" />. The cluster identity will be activated if it is not already.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct) =>
        IdentityLookup.GetAsync(clusterIdentity, ct);

    /// <summary>
    ///     Sends a request to a virtual actor.
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity of the actor</param>
    /// <param name="message">Message to send</param>
    /// <param name="context">Sender context to send the message through</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <typeparam name="T">Expected response type</typeparam>
    /// <returns>Response of null if timed out</returns>
    public Task<T> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context,
        CancellationToken ct) =>
        ClusterContext.RequestAsync<T>(clusterIdentity, message, context, ct)!;

    public ActivatedClusterKind GetClusterKind(string kind)
    {
        if (!_clusterKinds.TryGetValue(kind, out var clusterKind))
        {
            throw new ArgumentException($"No cluster kind '{kind}' was not found");
        }

        return clusterKind;
    }

    internal ActivatedClusterKind? TryGetClusterKind(string kind)
    {
        _clusterKinds.TryGetValue(kind, out var clusterKind);

        return clusterKind;
    }

    /// <summary>
    ///     Gets cluster identity for specified identity and kind. <see cref="PID" /> is attached to this cluster identity if
    ///     available in <see cref="PidCache" />
    /// </summary>
    /// <param name="identity">Identity</param>
    /// <param name="kind">Cluster kidn</param>
    /// <returns></returns>
    public ClusterIdentity GetIdentity(string identity, string kind)
    {
        var id = new ClusterIdentity
        {
            Identity = identity,
            Kind = kind
        };

        if (PidCache.TryGet(id, out var pid))
        {
            id.CachedPid = pid;
        }

        return id;
    }
}
