using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

/// <summary>
/// Builder for defining gossip consensus checks across cluster members.
/// </summary>
/// <typeparam name="T">Type of the value that should be in agreement.</typeparam>
public class ConsensusCheckBuilder<T> : IConsensusCheckDefinition<T>
    where T : notnull
{
    private static readonly ILogger Logger = Log.CreateLogger<ConsensusCheckBuilder<T>>();
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

    private static Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, T value)> MapToValue(
        (string, Func<Any, T?>) valueTuple)
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
        static void LogConsensus(bool consensus, IEnumerable<(string member, string key, T value)> tuples)
        {
            Logger.LogDebug("consensus {Consensus}: {Values}", consensus, tuples
                .GroupBy(it => (it.key, it.value), tuple => tuple.member)
                .Select(grouping => $"{grouping.Key.key}:{grouping.Key.value}, " +
                                     (grouping.Count() > 1 ? grouping.Count() + " nodes" : grouping.First()))
                .ToArray());
        }

        if (_getConsensusValues.Count == 1)
        {
            var mapToValue = MapToValue(_getConsensusValues.Single());

            return (state, ids) =>
            {
                var (consensus, value, tuples) = ConsensusEvaluator.HasConsensus(state, ids, new[] { mapToValue });

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    LogConsensus(consensus, tuples);
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
                LogConsensus(consensus, tuples);
            }

            return consensus ? (consensus, value!) : default;
        };
    }
}
