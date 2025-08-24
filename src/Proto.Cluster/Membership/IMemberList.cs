namespace Proto.Cluster;

using System.Collections.Immutable;

public interface IMemberList
{
    bool ContainsMemberId(string memberId);
    bool TryGetMember(string memberId, out Member? value);
    Member Self { get; }
    ImmutableHashSet<string> GetMembers();
}
