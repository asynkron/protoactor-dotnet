// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public delegate (bool, T) ConsensusCheck<T>(GossipState state, IImmutableSet<string> memberIds);

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

    public record GetGossipStateSnapshot();

    public class Gossiper
    {
        public const string GossipActorName = "gossip";
        private readonly Cluster _cluster;
        private readonly RootContext _context;

        private static readonly ILogger Logger = Log.CreateLogger<Gossiper>();
        private PID _pid = null!;

        public Task<GossipState> GetStateSnapshot() => _context.RequestAsync<GossipState>(_pid, new GetGossipStateSnapshot());

        public Gossiper(Cluster cluster)
        {
            _cluster = cluster;
            _context = _cluster.System.Root;
        }

        public async Task<ImmutableDictionary<string, T>> GetState<T>(string key) where T : IMessage, new()
        {
            _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

            var res = await _context.RequestAsync<GetGossipStateResponse>(_pid, new GetGossipStateRequest(key));

            var dict = res.State;
            var typed = ImmutableDictionary<string, T>.Empty;

            foreach (var (k, value) in dict)
            {
                typed = typed.SetItem(k, value.Unpack<T>());
            }

            return typed;
        }
        
        public async Task<ImmutableDictionary<string, GossipKeyValue>> GetStateEntry(string key) 
        {
            _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

            var res = await _context.RequestAsync<GetGossipStateEntryResponse>(_pid, new GetGossipStateEntryRequest(key));

            return res.State;
        }

        // Send message to update member state
        // Will not wait for completed state update
        public void SetState(string key, IMessage value)
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
            _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

            if (_pid == null)
            {
                return;
            }

            _context.Send(_pid, new SetGossipStateKey(key, value));
        }

        public Task SetStateAsync(string key, IMessage value)
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
            _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

            if (_pid == null)
            {
                return Task.CompletedTask;
            }

            return _context.RequestAsync<SetGossipStateResponse>(_pid, new SetGossipStateKey(key, value));
        }

        internal Task StartAsync()
        {
            var props = Props.FromProducer(() => new GossipActor(_cluster.Config.GossipRequestTimeout, _context.System.Id, () => _cluster.Remote.BlockList.BlockedMembers, _cluster.System.Logger(),_cluster.Config.GossipFanout, _cluster.Config.GossipMaxSend));
            _pid = _context.SpawnNamed(props, GossipActorName);
            _cluster.System.EventStream.Subscribe<ClusterTopology>(topology => _context.Send(_pid, topology));
            Logger.LogInformation("Started Cluster Gossip");
            _ = SafeTask.Run(GossipLoop);

            return Task.CompletedTask;
        }

        private async Task GossipLoop()
        {
            Logger.LogInformation("Starting gossip loop");
            await Task.Yield();

            while (!_cluster.System.Shutdown.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cluster.Config.GossipInterval);
                    
                    await BlockExpiredHeartbeats();

                    await BlockGracefullyLeft();

                    await SetStateAsync(GossipKeys.Heartbeat, new MemberHeartbeat()
                    {
                        ActorStatistics = GetActorStatistics()
                    });

                    await SendStateAsync();
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "Gossip loop failed");
                }
            }
        }

        private async Task BlockGracefullyLeft()
        {
            var t2 = await GetStateEntry(GossipKeys.GracefullyLeft);

            //don't ban ourselves. our gossip state will never reach other members then...
            var gracefullyLeft = t2.Keys.Where(k => k != _cluster.System.Id).ToArray();

            if (gracefullyLeft.Any())
            {
                Logger.LogInformation("Blocking members due to gracefully leaving {Members}", gracefullyLeft);
                _cluster.MemberList.UpdateBlockedMembers(gracefullyLeft);
            }
        }

        private async Task BlockExpiredHeartbeats()
        {
            var t = await GetStateEntry(GossipKeys.Heartbeat);

            var blocked = (from x in t
                           where x.Value.Age > _cluster.Config.HeartbeatExpiration
                           select x.Key)
                .ToArray();

            if (blocked.Any())
            {
                Logger.LogInformation("Blocking members due to expired heartbeat {Members}", blocked);
                _cluster.MemberList.UpdateBlockedMembers(blocked);
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

        public class ConsensusCheckBuilder<T>: IConsensusCheckDefinition<T>
        {
            private readonly ImmutableList<(string, Func<Any, T?>)> _getConsensusValues;

            private readonly Lazy<ConsensusCheck<T>> _check;
            public ConsensusCheck<T> Check => _check.Value;

            public IImmutableSet<string> AffectedKeys => _getConsensusValues.Select(it => it.Item1).ToImmutableHashSet();

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

            public static ConsensusCheckBuilder<T> Create<TE>(string key, Func<TE, T?> getValue) where TE : IMessage, new()
                => new(key, MapFromAny(getValue));

            private static Func<Any, T?> MapFromAny<TE>(Func<TE, T?> getValue) where TE : IMessage, new()
                => any => any.TryUnpack<TE>(out var envelope) ? getValue(envelope) : default;

            public ConsensusCheckBuilder<T> InConsensusWith<TE>(string key, Func<TE, T> getValue) where TE : IMessage, new()
                => new(_getConsensusValues.Add((key, MapFromAny(getValue))));

            private static Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, T value)> MapToValue(
                (string, Func<Any, T?>) valueTuple
            )
            {
                var (key, unpack) = valueTuple;
                return (kv) => {
                    var (member, state) = kv;
                    var value = state.Values.TryGetValue(key, out var any) ? unpack(any.Value) : default;
                    return (member, key, value);
                };
            }

            private ConsensusCheck<T> Build()
            {
                if (_getConsensusValues.Count == 1)
                {
                    var mapToValue = MapToValue(_getConsensusValues.Single());
                    return (state, ids) => {
                        var memberStates = GetValidMemberStates(state, ids);

                        // Missing state, cannot have consensus
                        if (memberStates.Length < ids.Count)
                        {
                            return default;
                        }

                        var valueTuples = memberStates.Select(mapToValue);
                        // ReSharper disable PossibleMultipleEnumeration
                        var result = valueTuples.Select(it => it.value).HasConsensus();

                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("consensus {Consensus}: {Values}", result.Item1, valueTuples
                                .GroupBy(it => (it.key, it.value), tuple => tuple.member).Select(
                                    grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                                (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                                )
                            );
                        }

                        return result;
                    };
                }

                var mappers = _getConsensusValues.Select(MapToValue).ToArray();

                return (state, ids) => {
                    var memberStates = GetValidMemberStates(state, ids);

                    if (memberStates.Length < ids.Count) // Not all members have state..
                    {
                        return default;
                    }

                    var valueTuples = memberStates
                        .SelectMany(memberState => mappers.Select(mapper => mapper(memberState)));
                    var consensus = valueTuples.Select(it => it.value).HasConsensus();

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("consensus {Consensus}: {Values}", consensus.Item1, valueTuples
                            .GroupBy(it => (it.key, it.value), tuple => tuple.member).Select(
                                grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                            (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First())
                            )
                        );
                    }

                    // ReSharper enable PossibleMultipleEnumeration
                    return consensus;
                };

                KeyValuePair<string, GossipState.Types.GossipMemberState>[] GetValidMemberStates(GossipState state, IImmutableSet<string> ids)
                    => state.Members
                        .Where(member => ids.Contains(member.Key))
                        .Select(member => member).ToArray();
            }
        }

        public IConsensusHandle<TV> RegisterConsensusCheck<T, TV>(string key, Func<T, TV?> getValue) where T : notnull, IMessage, new()
            => RegisterConsensusCheck(ConsensusCheckBuilder<TV>.Create(key, getValue));


        public IConsensusHandle<T> RegisterConsensusCheck<T>(IConsensusCheckDefinition<T> consensusDefinition) where T : notnull
        {
            var cts = new CancellationTokenSource();
            var (consensusHandle, check) = consensusDefinition.Build(cts.Cancel);
            _context.Send(_pid, new AddConsensusCheck(check, cts.Token));

            return consensusHandle;
        }
        
        private async Task SendStateAsync()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (_pid == null)
            {
                //just make sure a cluster client cant send
                return;
            }

            try
            {
                await _context.RequestAsync<SendGossipStateResponse>(_pid, new SendGossipStateRequest(), CancellationTokens.FromSeconds(5));
            }
            catch (DeadLetterException)
            {
            }
            catch (OperationCanceledException)
            {
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                //TODO: log
            }
        }

        internal Task ShutdownAsync()
        {
            Logger.LogInformation("Shutting down heartbeat");
            _context.Stop(_pid);
            Logger.LogInformation("Shut down heartbeat");
            return Task.CompletedTask;
        }
    }
}