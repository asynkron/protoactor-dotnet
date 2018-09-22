// -----------------------------------------------------------------------
//   <copyright file="WeightedMemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Cluster.WeightedMemberStrategy
{
    public class WeightedMemberStrategy : IMemberStrategy
    {
        private List<MemberStatus> _members;
        private Rendezvous _rdv;
        private WeightedRoundRobin _wrr;

        public WeightedMemberStrategy()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous(this);
            _wrr = new WeightedRoundRobin(this);
        }

        public List<MemberStatus> GetAllMembers() => _members;

        public void AddMember(MemberStatus member)
        {
            _members.Add(member);
            _wrr.UpdateRR();
            _rdv.UpdateRdv();
        }

        public void UpdateMember(MemberStatus member)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Address == member.Address)
                {
                    _members[i] = member;
                    _wrr.UpdateRR();
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
                    _wrr.UpdateRR();
                    _rdv.UpdateRdv();
                    return;
                }
            }
        }

        public string GetPartition(string key) => _rdv.GetNode(key);

        public string GetActivator() => _wrr.GetNode();
    }
}
