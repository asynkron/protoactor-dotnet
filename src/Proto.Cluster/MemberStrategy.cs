// -----------------------------------------------------------------------
//   <copyright file="DefaultMemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
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

    public interface IMemberStrategyProvider
    {
        IMemberStrategy GetMemberStrategy(string kind);
    }

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

        public List<MemberStatus> GetAllMembers() => members;

        public void AddMember(MemberStatus member)
        {
            members.Add(member);
            wrr.UpdateRR();
            rdv.UpdateRdv();
        }

        public void UpdateMember(MemberStatus member)
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