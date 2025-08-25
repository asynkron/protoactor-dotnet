using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

public partial class Gossiper
{
    /// <summary>
    ///     Gets the current full gossip state as seen by current member
    /// </summary>
    public Task<GossipState> GetStateSnapshot() =>
        _context.RequestAsync<GossipState>(_pid, new GetGossipStateSnapshot());

    /// <summary>
    ///     Gets gossip state entry by key, for each member represented in the gossip state, as seen by current member
    /// </summary>
    /// <typeparam name="T">Dictionary where member id is the key and gossip state value is the value</typeparam>
    public async Task<ImmutableDictionary<string, T>> GetState<T>(string key) where T : IMessage, new()
    {
        _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

        try
        {
            var res = await _context.RequestAsync<GetGossipStateResponse>(_pid, new GetGossipStateRequest(key)).ConfigureAwait(false);

            var dict = res.State;
            var typed = dict.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Unpack<T>());

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
    ///     Dictionary where member id is the key and gossip state value is the value, wrapped in <see cref="GossipKeyValue" />
    /// </param>
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
    ///     Sets a gossip state key to provided value. This will not wait for the state to be actually updated in current member's gossip state.
    /// </summary>
    public void SetState(string key, IMessage value)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
        }

        _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);

        if (_pid == null)
        {
            Logger.GossiperNotStartedCannotSetState(key);
            return;
        }

        _context.Send(_pid, new SetGossipStateKey(key, value));
    }

    /// <summary>
    ///     Sets a gossip state key to provided value. Waits for the state to be updated in current member's gossip state.
    /// </summary>
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
}

