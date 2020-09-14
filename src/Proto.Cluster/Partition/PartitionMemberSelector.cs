using System.Collections.Generic;

namespace Proto.Cluster.Partition
{
    //this class is responsible for translating between Identity->member
    //this is the key algorithm for the distributed hash table
    internal class PartitionMemberSelector
    {
        private readonly object _lock = new object();
        private readonly List<MemberInfo> _members = new List<MemberInfo>();
        private readonly Rendezvous _rdv = new Rendezvous();

        public int Count => _members.Count;


        public void Update(MemberInfo[] members)
        {
            lock (_lock)
            {
                _rdv.UpdateMembers(members);
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