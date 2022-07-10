// -----------------------------------------------------------------------
// <copyright file = "SimpleMemberStrategy.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster;

class RoundRobinMemberStrategy : IMemberStrategy
{
    private readonly RoundRobinMemberSelector _selector;
    private ImmutableList<Member> _members = ImmutableList<Member>.Empty;

    public RoundRobinMemberStrategy() => _selector = new RoundRobinMemberSelector(this);

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