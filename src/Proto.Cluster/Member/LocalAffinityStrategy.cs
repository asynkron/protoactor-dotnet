// -----------------------------------------------------------------------
// <copyright file="LocalAffinityStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Immutable;
using System.Linq;
using Proto.Cluster.Partition;

namespace Proto.Cluster
{
    /// <summary>
    ///     Prioritizes placement on current node, to optimize performance on partitioned workloads
    /// </summary>
    class LocalAffinityStrategy : IMemberStrategy
    {
        private readonly Cluster _cluster;
        private readonly Rendezvous _rdv;
        private readonly RoundRobinMemberSelector _rr;
        private Member? _me;
        private ImmutableList<Member> _members = ImmutableList<Member>.Empty;

        public LocalAffinityStrategy(Cluster cluster)
        {
            _cluster = cluster;
            _rdv = new Rendezvous();
            _rr = new RoundRobinMemberSelector(this);
        }

        public ImmutableList<Member> GetAllMembers() => _members;

        public void AddMember(Member member)
        {
            // Avoid adding the same member twice
            if (_members.Any(x => x.Address == member.Address)) return;

            if (member.Address.Equals(_cluster.System.Address)) _me = member;
            _members = _members.Add(member);
            _rdv.UpdateMembers(_members);
        }

        public void RemoveMember(Member member)
        {
            _members = _members.RemoveAll(x => x.Address == member.Address);
            _rdv.UpdateMembers(_members);
        }

        public Member? GetActivator(string senderAddress)
        {
            if (_me?.Address.Equals(senderAddress) == true) return _me;

            var sender = _members.FirstOrDefault(member => member.Address == senderAddress);
            //TODO: Verify that the member is not overloaded already
            return sender ?? _rr.GetMember();
        }
    }
}