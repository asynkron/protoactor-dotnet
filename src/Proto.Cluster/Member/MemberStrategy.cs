// -----------------------------------------------------------------------
//   <copyright file="MemberStrategy.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
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

        Member? GetActivator();
    }

    internal class SimpleMemberStrategy : IMemberStrategy
    {
        private ImmutableList<Member> Members { get; init; }
        private readonly Rendezvous _rdv;
        private readonly RoundRobinMemberSelector _rr;

        public SimpleMemberStrategy()
        {
            Members = ImmutableList<Member>.Empty;
            _rdv = new Rendezvous();
            _rr = new RoundRobinMemberSelector(this);
        }

        public ImmutableList<Member> GetAllMembers() => Members;

        //TODO: account for Member.MemberId
        public void AddMember(Member member)
        {
            // Avoid adding the same member twice
            if (Members.Any(x => x.Address == member.Address))
            {
                return;
            }

            Members.Add(member);
            _rdv.UpdateMembers(Members);
        }

        //TODO: account for Member.MemberId
        public void RemoveMember(Member member)
        {
            Members.RemoveAll(x => x.Address == member.Address);
            _rdv.UpdateMembers(Members);
        }

        public string GetActivatorAddress() => _rr.GetMemberAddress();
        public Member? GetActivator() => _rr.GetMember();
    }
}