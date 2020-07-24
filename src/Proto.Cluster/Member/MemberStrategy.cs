// -----------------------------------------------------------------------
//   <copyright file="MemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster
{
    public interface IMemberStrategy
    {
        List<MemberStatus> GetAllMembers();
        void AddMember(MemberStatus member);
        void UpdateMember(MemberStatus member);
        void RemoveMember(MemberStatus member);
        string GetPartition(string key);
        string GetActivator();
    }

    class SimpleMemberStrategy : IMemberStrategy
    {
        private readonly List<MemberStatus> _members;
        private readonly Rendezvous _rdv;
        private readonly RoundRobinMemberSelector _rr;

        public SimpleMemberStrategy()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous();
            _rr = new RoundRobinMemberSelector(this);
        }

        public List<MemberStatus> GetAllMembers() => _members;

        public void AddMember(MemberStatus member)
        {
            // Avoid adding the same member twice
            if (_members.Any(x => x.Address == member.Address)) return;
            
            _members.Add(member);
            _rdv.UpdateMembers(_members);
        }
        
        public void UpdateMember(MemberStatus member)
        {
            _members.RemoveAll(x => x.Address == member.Address);
            _members.Add(member);
        }
        
        public void RemoveMember(MemberStatus member)
        {
            _members.RemoveAll(x => x.Address == member.Address);
            _rdv.UpdateMembers(_members);
        }

        public string GetPartition(string key) => _rdv.GetOwnerMemberByIdentity(key);

        public string GetActivator() => _rr.GetMember();
    }
}