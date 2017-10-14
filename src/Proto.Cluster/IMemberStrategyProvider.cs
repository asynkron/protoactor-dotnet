using System.Collections.Generic;

namespace Proto.Cluster
{
    public interface IMemberStrategy
    {
        bool HasNoMember();
        List<MemberStatus> GetAllMembers();
        void AddMember(MemberStatus member);
        void RemoveMember(MemberStatus member);
        string GetPartition(string key);
        string GetActivator();
    }

    public interface IMemberStrategyProvider
    {
        IMemberStrategy GetMemberStrategy(string kind);
    }
}