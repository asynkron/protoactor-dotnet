using System.Collections.Generic;
using System.Linq;
using Proto.Cluster.Data;

namespace Proto.Cluster.Partition
{
    //this class is responsible for translating between Identity->member
    //this is the key algorithm for the distributed hash table
    internal class PartitionMemberSelector
    {
        private readonly object _lock = new object();
        private readonly List<MemberInfo> _members;
        private readonly Rendezvous _rdv;

        public PartitionMemberSelector()
        {
            _members = new List<MemberInfo>();
            _rdv = new Rendezvous();
        }

        public int Count => _members.Count;


        //TODO: account for Member.MemberId
        public void AddMember(MemberInfo member)
        {
            lock (_lock)
            {
                // Avoid adding the same member twice
                if (_members.Any(x => x.Address == member.Address))
                {
                    return;
                }

                _members.Add(member);
                _rdv.UpdateMembers(_members);
            }
        }

        //TODO: account for Member.MemberId
        public void RemoveMember(MemberInfo member)
        {
            lock (_lock)
            {
                _members.RemoveAll(x => x.Address == member.Address);
                _rdv.UpdateMembers(_members);
            }
        }

        public string GetIdentityOwner(string key)
        {
            lock (_lock)
            {
                return _rdv.GetOwnerMemberByIdentity(key);
            }
        }
    }
}