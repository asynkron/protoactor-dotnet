// -----------------------------------------------------------------------
//   <copyright file="DefaultMemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Cluster
{
    public class MemberStrategyProvider : IMemberStrategyProvider
    {
        public IMemberStrategy GetMemberStrategy(string kind) => new MemberStrategy();
    }

    public class MemberStrategy : IMemberStrategy
    {
        internal List<MemberStatus> members;
        private Rendezvous rdv;
        private WeightedRoundRobin wrr;

        public MemberStrategy()
        {
            members = new List<MemberStatus>();
            rdv = new Rendezvous(this);
            wrr = new WeightedRoundRobin(this);
        }

        public bool HasNoMember() => members.Count == 0;

        public List<MemberStatus> GetAllMembers() => members;

        public void AddMember(MemberStatus member)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Address == member.Address)
                {
                    members[i] = member;
                    wrr.UpdateRR();
                    return;
                }
            }
            members.Add(member);
            wrr.UpdateRR();
            rdv.UpdateRdv();
        }

        public void RemoveMember(MemberStatus member)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Address == member.Address)
                {
                    members.RemoveAt(i);
                    wrr.UpdateRR();
                    rdv.UpdateRdv();
                    return;
                }
            }
        }

        public string GetPartition(string key) => rdv.GetNode(key);

        public string GetActivator() => wrr.GetNode();
    }
}