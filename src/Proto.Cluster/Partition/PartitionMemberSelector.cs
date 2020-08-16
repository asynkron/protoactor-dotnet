using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.Partition
{
    
    //this class is responsible for translating between Identity->member
    //this is the key algorithm for the distributed hash table
    internal class PartitionMemberSelector 
    {
        private readonly List<MemberStatus> _members;
        private readonly Rendezvous _rdv;
        private readonly object _lock = new object();

        public int Count => _members.Count;

        public PartitionMemberSelector()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous();
        }
        

        //TODO: account for Member.MemberId
        public void AddMember(MemberStatus member)
        {
            lock (_lock)
            {
                // Avoid adding the same member twice
                if (_members.Any(x => x.Address == member.Address)) return;

                _members.Add(member);
                _rdv.UpdateMembers(_members);
            }
        }
        
        //TODO: account for Member.MemberId
        public void RemoveMember(MemberStatus member)
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