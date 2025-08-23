using System.Collections.Generic;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Tests;

internal sealed class DeterministicRandomProvider : IRandomProvider
{
    private readonly Queue<int> _values;

    public DeterministicRandomProvider(IEnumerable<int> values)
    {
        _values = new Queue<int>(values);
    }

    public int Next() => _values.Count > 0 ? _values.Dequeue() : 0;
}
