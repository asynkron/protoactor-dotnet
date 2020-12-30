// -----------------------------------------------------------------------
// <copyright file="RoundRobinMemberSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Cluster
{
    public class RoundRobinMemberSelector
    {
        private readonly IMemberStrategy _memberStrategy;
        private int _val;

        public RoundRobinMemberSelector(IMemberStrategy memberStrategy) => _memberStrategy = memberStrategy;

        public string GetMemberAddress()
        {
            var members = _memberStrategy.GetAllMembers();
            var l = members.Count;

            switch (l)
            {
                case 0: return "";
                case 1: return members[0].Address;
                default: {
                    var nv = Interlocked.Increment(ref _val);
                    return members[nv % l].Address;
                }
            }
        }

        public Member? GetMember()
        {
            var members = _memberStrategy.GetAllMembers();
            var l = members.Count;

            switch (l)
            {
                case 0: return null;
                case 1: return members[0];
                default: {
                    var nv = Interlocked.Increment(ref _val);
                    return members[nv % l];
                }
            }
        }
    }
}