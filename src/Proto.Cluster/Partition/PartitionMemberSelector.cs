// -----------------------------------------------------------------------
// <copyright file="PartitionMemberSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Partition
{
    //this class is responsible for translating between Identity->member
    //this is the key algorithm for the distributed hash table
    class PartitionMemberSelector
    {
        private readonly object _lock = new();
        private readonly Rendezvous _rdv = new();

        public void Update(Member[] members)
        {
            lock (_lock) _rdv.UpdateMembers(members);
        }

        public string GetIdentityOwner(string key)
        {
            lock (_lock) return _rdv.GetOwnerMemberByIdentity(key);
        }
    }
}