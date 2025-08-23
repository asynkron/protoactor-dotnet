namespace Proto.Cluster;

public interface IMemberList
{
    bool ContainsMemberId(string memberId);
    bool TryGetMember(string memberId, out Member? value);
    Member Self { get; }
}

