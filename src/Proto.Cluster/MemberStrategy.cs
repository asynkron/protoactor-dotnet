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

    internal class SimpleMemberStrategy : IMemberStrategy
    {
        private List<MemberStatus> _members;
        private Rendezvous _rdv;
        private RoundRobin _rr;

        public SimpleMemberStrategy()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous(this);
            _rr = new RoundRobin(this);
        }

        public List<MemberStatus> GetAllMembers() => _members;

        public void AddMember(MemberStatus member)
        {
            _members.Add(member);
            _rdv.UpdateRdv();
        }

        public void UpdateMember(MemberStatus member)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Address == member.Address)
                {
                    _members[i] = member;
                    return;
                }
            }
        }

        public void RemoveMember(MemberStatus member)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Address == member.Address)
                {
                    _members.RemoveAt(i);
                    _rdv.UpdateRdv();
                    return;
                }
            }
        }

        public string GetPartition(string key) => _rdv.GetNode(key);

        public string GetActivator() => _rr.GetNode();
    }
}