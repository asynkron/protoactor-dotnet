// -----------------------------------------------------------------------
//   <copyright file="MemberStrategy.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
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
        private List<MemberStatus> members;
        private Rendezvous rdv;
        private RoundRobin rr;

        public SimpleMemberStrategy()
        {
            members = new List<MemberStatus>();
            rdv = new Rendezvous(this);
            rr = new RoundRobin(this);
        }

        public List<MemberStatus> GetAllMembers() => members;

        public void AddMember(MemberStatus member)
        {
            members.Add(member);
            rdv.UpdateRdv();
        }

        public void UpdateMember(MemberStatus member)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Address == member.Address)
                {
                    members[i] = member;
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
                    rdv.UpdateRdv();
                    return;
                }
            }
        }

        public string GetPartition(string key) => rdv.GetNode(key);

        public string GetActivator() => rr.GetNode();
    }
}