using System.Collections.Immutable;

namespace Proto.Remote;

public interface IBlockList
{
    ImmutableHashSet<string> BlockedMembers { get; }
}
