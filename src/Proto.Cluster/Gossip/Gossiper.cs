// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Remote;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace Proto.Cluster.Gossip;

public delegate (bool, T) ConsensusCheck<T>(GossipState state, IImmutableSet<string> memberIds) where T : notnull;

public record GossipUpdate(string MemberId, string Key, Any Value, long SequenceNumber);

public record GetGossipStateRequest(string Key);

public record GetGossipStateResponse(ImmutableDictionary<string, Any> State);

public record GetGossipStateEntryRequest(string Key);

public record GetGossipStateEntryResponse(ImmutableDictionary<string, GossipKeyValue> State);

public record SetGossipStateKey(string Key, IMessage Value);

public record SetGossipStateResponse;

public record SendGossipStateRequest;

public record SendGossipStateResponse;

public record AddConsensusCheck(ConsensusCheck Check, CancellationToken Token);

public record GetGossipStateSnapshot;

[PublicAPI]
public class Gossiper
{
    public const string GossipActorName = "$gossip";

#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<Gossiper>();
#pragma warning restore CS0618 // Type or member is obsolete
    private readonly Cluster _cluster;
    private readonly IRootContext _context;
    private IGossip _gossip = null!;
    private PID _pid = null!;

    public Gossiper(Cluster cluster)
    {
        _cluster = cluster;
        _context = _cluster.System.Root;
    }

    /// <summary>
    ///     Gets the current full gossip state as seen by current member
    /// </summary>
    /// <returns></returns>
    public Task<GossipState> GetStateSnapshot() =>
        _context.RequestAsync<GossipState>(_pid, new GetGossipStateSnapshot());

    /// <summary>
    ///     Gets gossip state entry by key, for each member represented in the gossip state, as seen by current member
    /// </summary>
    /// <param name="key"></param>
    /// <typeparam name="T">Dictionary where member id is the key and gossip state value is the value</typeparam>
    /// <returns></returns>
    public async Task<ImmutableDictionary<string, T>> GetState<T>(string key) where T : IMessage, new()
    {
        _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

        try
        {
            var res = await _context.RequestAsync<GetGossipStateResponse>(_pid, new GetGossipStateRequest(key)).ConfigureAwait(false);

            var dict = res.State;
            var typed = ImmutableDictionary<string, T>.Empty;

            foreach (var (k, value) in dict)
            {
                typed = typed.SetItem(k, value.Unpack<T>());
            }

            return typed;
        }
        catch (DeadLetterException)
        {
            //pass, system is shutting down
        }

        return ImmutableDictionary<string, T>.Empty;
    }

    /// <summary>
    ///     Gets the gossip state entry by key, for each member represented in the gossip state, as seen by current member
    /// </summary>
    /// <param name="key">
    ///     Dictionary where member id is the key and gossip state value is the value, wrapped in
    ///     <see cref="GossipKeyValue" />
    /// </param>
    /// <returns></returns>
    public async Task<ImmutableDictionary<string, GossipKeyValue>> GetStateEntry(string key)
    {
        _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

        try
        {
            var res = await _context.RequestAsync<GetGossipStateEntryResponse>(_pid,
                new GetGossipStateEntryRequest(key), CancellationTokens.FromSeconds(5)).ConfigureAwait(false);

            return res.State;
        }
        catch (DeadLetterException)
        {
            //ignore, we are shutting down  
        }
        catch (Exception x)
        {
            Logger.LogError(x, "Failed to get gossip state entry");
        }

        return ImmutableDictionary<string, GossipKeyValue>.Empty;
    }

    /// <summary>
    ///     Sets a gossip state key to provided value. This will not wait for the state to be actually updated in current
    ///     member's gossip state.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void SetState(string key, IMessage value)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
        }

        _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

        if (_pid == null)
        {
            // Use generated logging for attempts to set state before startup
            Logger.GossiperNotStartedCannotSetState(key);
            return;
        }

        _context.Send(_pid, new SetGossipStateKey(key, value));
    }

    /// <summary>
    ///     Sets a gossip state key to provided value. Waits for the state to be updated in current member's gossip state.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public async Task SetStateAsync(string key, IMessage value)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
        }

        _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

        if (_pid == null)
        {
            return;
        }

        try
        {
            await _context.RequestAsync<SetGossipStateResponse>(_pid, new SetGossipStateKey(key, value)).ConfigureAwait(false);
        }
        catch (DeadLetterException)
        {
            //ignore, we are shutting down  
        }
    }

    internal async Task StartGossipActorAsync(IGossip? gossip = null, IGossipTransport? transport = null)
    {

        await Task.Delay(100); //racy. _cluster.MemberList may be null
        
        _gossip = gossip ?? new Gossip(
            _cluster.System.Id,
            _cluster.Config.GossipFanout,
            _cluster.Config.GossipMaxSend,
            _cluster.System.Logger(),
            () => _cluster.MemberList.GetMembers(),
            _cluster.Config.GossipDebugLogging);

        var props = Props.FromProducer(() => new GossipActor(
            _cluster.Config.GossipRequestTimeout,
            _gossip,
            transport ?? new GossipTransport(),
            _cluster.MemberList,
            _cluster.System.Remote().BlockList,
            _cluster.Config.GossipDebugLogging));

        _pid = _context.SpawnNamedSystem(props, GossipActorName);
        _cluster.System.EventStream.Subscribe<ClusterTopology>(topology =>
        {
            var tmp = topology.Clone();
            tmp.Joined.Clear();
            tmp.Left.Clear();
            _context.Send(_pid, tmp);
        });

    }

    internal Task StartgossipLoopAsync()
    {

        Logger.LogInformation("Started Cluster Gossip");
        _ = SafeTask.Run(GossipLoop);

        return Task.CompletedTask;
    }

    private async Task GossipLoop()
    {
        Logger.LogInformation("Gossip is waiting for cluster to join");
        await _cluster.JoinedCluster;
        Logger.LogInformation("Starting gossip loop");
        await Task.Yield();

        while (!_cluster.System.Shutdown.IsCancellationRequested)
        {
            try
            {
                // Space out gossip broadcasts according to configured interval
                await Task.Delay(_cluster.Config.GossipInterval).ConfigureAwait(false);

                await BlockExpiredHeartbeats().ConfigureAwait(false);

                await BlockGracefullyLeft().ConfigureAwait(false);

                await SetStateAsync(GossipKeys.Heartbeat, new MemberHeartbeat
                    {
                        ActorStatistics = GetActorStatistics()
                    }
                ).ConfigureAwait(false);

                await SendStateAsync().ConfigureAwait(false);
            }
            catch (DeadLetterException)
            {
                if (_cluster.System.Shutdown.IsCancellationRequested)
                {
                    //pass. this is expected, system is shutting down
                }
                else
                {
                    Logger.LogError("Gossip loop failed, Gossip actor has stopped");
                }
            }
            catch (Exception x)
            {
                x.CheckFailFast();
                Logger.LogWarning(x, "Gossip loop failed");
            }
        }
    }

    private async Task BlockGracefullyLeft()
    {
        var t2 = await GetStateEntry(GossipKeys.GracefullyLeft).ConfigureAwait(false);

        var blockList = _cluster.System.Remote().BlockList;
        var alreadyBlocked = blockList.BlockedMembers;

        //don't ban ourselves. our gossip state will never reach other members then...
        var gracefullyLeft = t2.Keys
            .Where(k => !alreadyBlocked.Contains(k))
            .Where(k => k != _cluster.System.Id)
            .ToArray();

        if (gracefullyLeft.Any())
        {
            blockList.Block(gracefullyLeft, "Gracefully left");
        }
    }

    private async Task BlockExpiredHeartbeats()
    {
        if (_cluster.Config.HeartbeatExpiration == TimeSpan.Zero)
        {
            return;
        }
        
        await _cluster.Config.HeartbeatExpirationHandler(_cluster);
    }

    public static async Task BlockExpiredMembers(Cluster cluster)
    {
        var gossipState = await cluster.Gossip. GetStateEntry(GossipKeys.Heartbeat).ConfigureAwait(false);
        var blockList = cluster.Remote.BlockList;
        var alreadyBlocked = blockList.BlockedMembers;
        //new blocked members
        var blocked = (from x in gossipState
                //never block ourselves
                where x.Key != cluster.System.Id
                //pick any entry that is too old
                where x.Value.Age > cluster.Config.HeartbeatExpiration
                //and not already part of the block list
                where !alreadyBlocked.Contains(x.Key)
                select x.Key)
            .ToArray();

        if (blocked.Any())
        {
            blockList.Block(blocked, "Expired heartbeat");
        }
    }

    private ActorStatistics GetActorStatistics()
    {
        var stats = new ActorStatistics();

        foreach (var k in _cluster.GetClusterKinds())
        {
            var kind = _cluster.GetClusterKind(k);
            stats.ActorCount.Add(k, kind.Count);
        }

        return stats;
    }

    /// <summary>
    ///     Helper for composing <see cref="ConsensusCheck{T}" /> logic over one or more gossip keys.
    /// </summary>
    /// <typeparam name="T">Type of the value that should be in consensus.</typeparam>
    /// <example>
    /// <code>
    /// var definition = new Gossiper.ConsensusCheckBuilder<int>("config", any => any.Unpack<Int32Value>().Value);
    /// var handle = gossiper.RegisterConsensusCheck(definition);
    /// </code>
    /// </example>
    public class ConsensusCheckBuilder<T> : IConsensusCheckDefinition<T>
        where T : notnull
    {
        private readonly Lazy<ConsensusCheck<T>> _check;
        private readonly ImmutableList<(string, Func<Any, T?>)> _getConsensusValues;

        private ConsensusCheckBuilder(ImmutableList<(string, Func<Any, T?>)> getValues)
        {
            _getConsensusValues = getValues;
            _check = new Lazy<ConsensusCheck<T>>(Build);
        }

        public ConsensusCheckBuilder(string key, Func<Any, T?> getValue)
        {
            _getConsensusValues = ImmutableList.Create<(string, Func<Any, T?>)>((key, getValue));
            _check = new Lazy<ConsensusCheck<T>>(Build, LazyThreadSafetyMode.PublicationOnly);
        }

        public ConsensusCheck<T> Check => _check.Value;

        public IImmutableSet<string> AffectedKeys => _getConsensusValues.Select(it => it.Item1).ToImmutableHashSet();

        public static ConsensusCheckBuilder<T> Create<TE>(string key, Func<TE, T?> getValue)
            where TE : IMessage, new() => new(key, MapFromAny(getValue));

        private static Func<Any, T?> MapFromAny<TE>(Func<TE, T?> getValue) where TE : IMessage, new() =>
            any => any.TryUnpack<TE>(out var envelope) ? getValue(envelope) : default;

        public ConsensusCheckBuilder<T> InConsensusWith<TE>(string key, Func<TE, T> getValue)
            where TE : IMessage, new() => new(_getConsensusValues.Add((key, MapFromAny(getValue))));

        private static Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, T
            value)> MapToValue(
            (string, Func<Any, T?>) valueTuple
        )
        {
            var (key, unpack) = valueTuple;

            return kv =>
            {
                var (member, state) = kv;
                var value = state.Values.TryGetValue(key, out var any) ? unpack(any.Value) : default;

                return (member, key, value!);
            };
        }

        private ConsensusCheck<T> Build()
        {
            if (_getConsensusValues.Count == 1)
            {
                var mapToValue = MapToValue(_getConsensusValues.Single());

                return (state, ids) =>
                {
                    var (consensus, value, tuples) = ConsensusEvaluator.HasConsensus(state, ids, new[] { mapToValue });

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("consensus {Consensus}: {Values}", consensus, tuples
                            .GroupBy(it => (it.key, it.value), tuple => tuple.member)
                            .Select(
                                grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                            (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                            ).ToArray()
                        );
                    }

                    return consensus ? (consensus, value!) : default;
                };
            }

            var mappers = _getConsensusValues.Select(MapToValue).ToArray();

            return (state, ids) =>
            {
                var (consensus, value, tuples) = ConsensusEvaluator.HasConsensus(state, ids, mappers);

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("consensus {Consensus}: {Values}", consensus, tuples
                        .GroupBy(it => (it.key, it.value), tuple => tuple.member)
                        .Select(
                            grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                        (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                        ).ToArray()
                    );
                }

                return consensus ? (consensus, value!) : default;
            };
        }
    }

    public IConsensusHandle<TV> RegisterConsensusCheck<T, TV>(string key, Func<T, TV?> getValue)
        where T : notnull, IMessage, new()
        where TV : notnull =>
        RegisterConsensusCheck(ConsensusCheckBuilder<TV>.Create(key, getValue));

    public IConsensusHandle<T> RegisterConsensusCheck<T>(IConsensusCheckDefinition<T> consensusDefinition)
        where T : notnull
    {
        var cts = new CancellationTokenSource();
        var (consensusHandle, check) = consensusDefinition.Build(cts.Cancel);
        _context.Send(_pid, new AddConsensusCheck(check, cts.Token));

        return consensusHandle;
    }

    private async Task SendStateAsync()
    {
        if (_pid == null)
        {
            //just make sure a cluster client cant send
            return;
        }

        try
        {
            await _context.RequestAsync<SendGossipStateResponse>(_pid, new SendGossipStateRequest(),
                CancellationTokens.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (DeadLetterException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception x)
        {
            x.CheckFailFast();
        }
    }

    internal async Task ShutdownAsync()
    {
        // _pid will be null when cluster started as "client"
        if (_pid == null)
        {
            return;
        }

        Logger.LogInformation("Shutting down heartbeat");
        await _context.StopAsync(_pid).ConfigureAwait(false);
        Logger.LogInformation("Shut down heartbeat");
    }
}
