// -----------------------------------------------------------------------
// <copyright file="MemberStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Immutable;
using System.Linq;
using Proto.Cluster.Partition;

namespace Proto.Cluster
{
    public interface IMemberStrategy
    {
        ImmutableList<Member> GetAllMembers();

        void AddMember(Member member);

        void RemoveMember(Member member);

        Member? GetActivator(string senderAddress);
    }

    class SimpleMemberStrategy : IMemberStrategy
    {
        private readonly RoundRobinMemberSelector _selector;
        private ImmutableList<Member> _members = ImmutableList<Member>.Empty;

        public SimpleMemberStrategy() => _selector = new RoundRobinMemberSelector(this);

        public ImmutableList<Member> GetAllMembers() => _members;

        //TODO: account for Member.MemberId
        public void AddMember(Member member)
        {
            // Avoid adding the same member twice
            if (_members.Any(x => x.Address == member.Address)) return;

            _members = _members.Add(member);
        }

        //TODO: account for Member.MemberId
        public void RemoveMember(Member member) => _members = _members.RemoveAll(x => x.Address == member.Address);

        public Member? GetActivator(string senderAddress) => _selector.GetMember();
    }
}