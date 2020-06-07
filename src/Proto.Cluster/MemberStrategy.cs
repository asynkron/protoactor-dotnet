// -----------------------------------------------------------------------
//   <copyright file="MemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

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
        private readonly RoundRobin _rr;

        public SimpleMemberStrategy()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous();
            _rr = new RoundRobin(this);
        }

        public List<MemberStatus> GetAllMembers() => _members;

        public void AddMember(MemberStatus member)
        {
            _members.Add(member);
            _rdv.UpdateMembers(_members);
        }

        public void UpdateMember(MemberStatus member)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Address != member.Address) continue;

                _members[i] = member;
                return;
            }
        }

        public void RemoveMember(MemberStatus member)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Address != member.Address) continue;

                _members.RemoveAt(i);
                _rdv.UpdateMembers(_members);
                return;
            }
        }

        public string GetPartition(string key) => _rdv.GetNode(key);

        public string GetActivator() => _rr.GetNode();
    }
}