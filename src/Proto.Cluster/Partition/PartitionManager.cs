// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster
{
    internal class IdentityMemberSelector 
    {
        private readonly List<MemberStatus> _members;
        private readonly Rendezvous _rdv;
        private readonly object _lock = new object();

        public int Count => _members.Count;

        public IdentityMemberSelector()
        {
            _members = new List<MemberStatus>();
            _rdv = new Rendezvous();
        }

        public List<MemberStatus> GetAllMembers() => _members;

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

        public string GetPartition(string key)
        {
            lock (_lock)
            {
                return _rdv.GetOwnerMemberByIdentity(key);
            }
        }
    }
    
    //helper to interact with partition actors on this and other members
    internal class PartitionManager
    {
        private PID _actor;
        private readonly Cluster _cluster;
        internal IdentityMemberSelector Selector { get; } = new IdentityMemberSelector();


        internal PartitionManager(Cluster cluster)
        {
            _cluster = cluster;
        }

        public void Setup()
        {
            SpawnPartitionActor();
        }

        private void SpawnPartitionActor()
        {
            _cluster.System.EventStream.Subscribe<MemberStatusEvent>(e =>
                {
                    _cluster.System.Root.Send(_actor,e);
                }
            );
            
            var props = Props
                .FromProducer(() => new PartitionActor(_cluster, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
             _actor = _cluster.System.Root.SpawnNamed(props, "partition-actor");
        }

        public void Stop()
        {
            _cluster.System.Root.Stop(_actor);
        }

        public PID RemotePartitionForKind(string address)
        {
            return new PID(address, "partition-actor");
        }
    }
}