using System.Collections.Generic;
using System.Linq;
using Proto.Cluster.Partition;

namespace Proto.Cluster
{
    /// <summary>
    /// Prioritizes placement on current node, to optimize performance on partitioned workloads
    /// </summary>
    public class LocalAffinityStrategy: IMemberStrategy
    {
        private Member? _me;
        private readonly Cluster _cluster;
        private readonly ProcessRegistry _registry;
        private readonly int _localAffinityActorLimit;
        private readonly List<Member> _members;
        private readonly Rendezvous _rdv;
        private readonly RoundRobinMemberSelector _rr;

        public LocalAffinityStrategy(Cluster cluster, int localAffinityActorLimit)
        {
            _cluster = cluster;
            _registry = cluster.System.ProcessRegistry;
            _localAffinityActorLimit = localAffinityActorLimit;
            _members = new List<Member>();
            _rdv = new Rendezvous();
            _rr = new RoundRobinMemberSelector(this);
        }

        public List<Member> GetAllMembers() => _members;

        public void AddMember(Member member)
        {
            // Avoid adding the same member twice
            if (_members.Any(x => x.Address == member.Address))
            {
                return;
            }

            if (member.Address.Equals(_cluster.System.Address))
            {
                _me = member;
            }
            _members.Add(member);
            _rdv.UpdateMembers(_members);
        }

        public void RemoveMember(Member member)
        {
            _members.RemoveAll(x => x.Address == member.Address);
            _rdv.UpdateMembers(_members);
        }

        public Member? GetActivator(string senderAddress)
        {
            if (_me?.Address.Equals(senderAddress) == true && _registry.ProcessCount < _localAffinityActorLimit)
            {
               return _me;
            }
            
            var sender = _members.FirstOrDefault(member => member.Address == senderAddress);
            //TODO: Verify that the member is not overloaded already
            return sender ?? _rr.GetMember();
        }
    }
}