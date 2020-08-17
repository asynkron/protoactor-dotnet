// -----------------------------------------------------------------------
//   <copyright file="MemberStrategy.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Proto.Cluster.Data;

namespace Proto.Cluster
{
    public interface IMemberStrategy
    {
        List<MemberInfo> GetAllMembers();
        void AddMember(MemberInfo member);

        void RemoveMember(MemberInfo member);
        string GetActivator();
    }

    internal class SimpleMemberStrategy : IMemberStrategy
    {
        private readonly List<MemberInfo> _members;
        private readonly Rendezvous _rdv;
        private readonly RoundRobinMemberSelector _rr;

        public SimpleMemberStrategy()
        {
            _members = new List<MemberInfo>();
            _rdv = new Rendezvous();
            _rr = new RoundRobinMemberSelector(this);
        }

        public int Count => _members.Count;

        public List<MemberInfo> GetAllMembers() => _members;

        //TODO: account for Member.MemberId
        public void AddMember(MemberInfo member)
        {
            // Avoid adding the same member twice
            if (_members.Any(x => x.Address == member.Address))
            {
                return;
            }

            _members.Add(member);
            _rdv.UpdateMembers(_members);
        }

        //TODO: account for Member.MemberId
        public void RemoveMember(MemberInfo member)
        {
            _members.RemoveAll(x => x.Address == member.Address);
            _rdv.UpdateMembers(_members);
        }

        public string GetActivator() => _rr.GetMember();

        public string GetPartition(string key) => _rdv.GetOwnerMemberByIdentity(key);
    }
}