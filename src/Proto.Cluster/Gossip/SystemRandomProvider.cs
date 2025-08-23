using System;

namespace Proto.Cluster.Gossip;

internal sealed class SystemRandomProvider : IRandomProvider
{
    private readonly Random _random;

    public SystemRandomProvider() : this(new Random())
    {
    }

    public SystemRandomProvider(Random random)
    {
        _random = random;
    }

    public int Next() => _random.Next();
}
