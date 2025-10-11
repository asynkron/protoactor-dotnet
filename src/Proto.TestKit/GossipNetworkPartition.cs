using System;
using System.Collections.Immutable;
using Proto;
using Proto.Cluster.Gossip;

namespace Proto.TestKit;

// Middleware to simulate network partitions for gossip messages in tests
public static class GossipNetworkPartition
{
    private static ImmutableHashSet<string> _isolated = ImmutableHashSet<string>.Empty;

    // Prevent gossip between the given address and any other nodes
    public static void Isolate(string address) => _isolated = _isolated.Add(address);

    // Clear all simulated partitions
    public static void Clear() => _isolated = ImmutableHashSet<string>.Empty;

    // Middleware that drops gossip requests to or from isolated nodes
    public static Func<Sender, Sender> Middleware => next => async (ctx, target, envelope) =>
    {
        if (envelope.Message is GossipRequest && target.Id == Gossiper.GossipActorName)
        {
            var from = ctx.System.Address;
            var to = target.Address;

            if (_isolated.Contains(from) || _isolated.Contains(to))
            {
                // message dropped due to simulated partition
                return;
            }
        }

        await next(ctx, target, envelope);
    };
}
