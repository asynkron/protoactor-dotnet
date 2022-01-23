// -----------------------------------------------------------------------
// <copyright file="ConsensusState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster.Gossip
{
    public record ConsensusCheck(string Id, Action<GossipState, ImmutableHashSet<string>> Check, string[] AffectedKeys);
    
    public class ConsensusChecks
    {
        private readonly Dictionary<string, ConsensusCheck> _consensusChecks = new();
        private readonly Dictionary<string, HashSet<string>> _affectedChecksByStateKey = new();

        public IEnumerable<ConsensusCheck> Get => _consensusChecks.Values;

        public IEnumerable<ConsensusCheck> GetByUpdatedKey(string key)
        {
            if (_affectedChecksByStateKey.TryGetValue(key, out var affectedIds))
            {
                return affectedIds.Select(id => _consensusChecks[id]);
            }
            return ImmutableList<ConsensusCheck>.Empty;
        }
        
        public IEnumerable<ConsensusCheck> GetByUpdatedKeys(IEnumerable<string> keys)
        {
            var ids = new HashSet<string>();

            foreach (var key in keys)
            {
                if (_affectedChecksByStateKey.TryGetValue(key, out var affectedIds))
                {
                    ids.UnionWith(affectedIds);
                }
            }

            return ids.Select(id => _consensusChecks[id]);
        }

        
        public void Add(ConsensusCheck consensusCheck)
        {
            _consensusChecks[consensusCheck.Id] = consensusCheck;
            RegisterAffectedKeys(consensusCheck.Id, consensusCheck.AffectedKeys);
        }
        
        public void Remove(string id)
        {
            if (_consensusChecks.Remove(id))
            {
                UnRegisterAffectedKeys(id);
            }
        }
     
        private void RegisterAffectedKeys(string id, string[] keys)
        {
            foreach (var key in keys)
            {
                if (_affectedChecksByStateKey.TryGetValue(key, out var affectedIds))
                {
                    affectedIds.Add(id);
                }
                else
                {
                    _affectedChecksByStateKey[key] = new HashSet<string> {id};
                }
            }
        }

        private void UnRegisterAffectedKeys(string id)
        {
            var empty = new HashSet<string>();

            foreach (var (key, ids) in _affectedChecksByStateKey)
            {
                if (ids.Remove(id) && ids.Count == 0)
                {
                    empty.Add(key);
                }
            }

            foreach (var key in empty)
            {
                _affectedChecksByStateKey.Remove(key);
            }
        }
    }
}