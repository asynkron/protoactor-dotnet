// -----------------------------------------------------------------------
// <copyright file="PidCache.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Proto.Remote;

namespace Proto.Cluster
{
    public class PidCache
    {
        private readonly ICollection<KeyValuePair<ClusterIdentity, PID>> _cacheCollection;
        private readonly ConcurrentDictionary<ClusterIdentity, PID> _cacheDict;

        public PidCache()
        {
            _cacheDict = new ConcurrentDictionary<ClusterIdentity, PID>();
            _cacheCollection = _cacheDict;
        }

        public bool TryGet(ClusterIdentity clusterIdentity, [NotNullWhen(true)] out PID? pid)
        {
            if (clusterIdentity is null) throw new ArgumentNullException(nameof(clusterIdentity));

            if (clusterIdentity.CachedPid is {CurrentRef: { } and not DeadLetterProcess} identityCachedPid)
            {
                //If the PID is already cached using ClusterIdentity, we can skip the lookup altogether
                pid = identityCachedPid;
                return true;
            }

            if (_cacheDict.TryGetValue(clusterIdentity, out pid))
            {
                clusterIdentity.CachedPid = pid;
                return true;
            }

            clusterIdentity.CachedPid = null;
            return false;
        }

        public bool TryAdd(ClusterIdentity clusterIdentity, PID pid)
        {
            if (clusterIdentity is null) throw new ArgumentNullException(nameof(clusterIdentity));

            if (pid is null) throw new ArgumentNullException(nameof(pid));

            if (!_cacheDict.TryAdd(clusterIdentity, pid)) return false;

            clusterIdentity.CachedPid = pid;
            return true;
        }

        public bool TryUpdate(ClusterIdentity clusterIdentity, PID newPid, PID existingPid)
        {
            if (clusterIdentity is null) throw new ArgumentNullException(nameof(clusterIdentity));

            if (newPid is null) throw new ArgumentNullException(nameof(newPid));

            if (existingPid is null) throw new ArgumentNullException(nameof(existingPid));

            if (!_cacheDict.TryUpdate(clusterIdentity, newPid, existingPid)) return false;

            clusterIdentity.CachedPid = newPid;
            return true;
        }

        public bool TryRemove(ClusterIdentity clusterIdentity)
        {
            if (clusterIdentity is null) throw new ArgumentNullException(nameof(clusterIdentity));

            clusterIdentity.CachedPid = null;
            return _cacheDict.TryRemove(clusterIdentity, out _);
        }

        public bool RemoveByVal(ClusterIdentity clusterIdentity, PID pid)
        {
            if (clusterIdentity.CachedPid?.Equals(pid) == true)
            {
                clusterIdentity.CachedPid = null;
            }

            if (_cacheDict.TryGetValue(clusterIdentity, out var existingPid) && existingPid.Id == pid.Id && existingPid.Address == pid.Address)
                return _cacheCollection.Remove(new KeyValuePair<ClusterIdentity, PID>(clusterIdentity, existingPid));

            return false;
        }

        /// <summary>
        /// Remove cached remote activations which have not been used since
        /// </summary>
        /// <param name="age"></param>
        /// <returns></returns>
        public int RemoveIdleRemoteProcessesOlderThan(TimeSpan age)
        {
            var cutoff = Stopwatch.GetTimestamp() - (long) (Stopwatch.Frequency * age.TotalSeconds);
            return RemoveByPredicate(pair => pair.Value.CurrentRef is RemoteProcess remoteProcess && remoteProcess.LastUsedTick < cutoff);
        }

        public int RemoveByMember(Member member)
            => RemoveByPredicate(pair => member.Address.Equals(pair.Value.Address, StringComparison.InvariantCulture));

        private int RemoveByPredicate(Func<KeyValuePair<ClusterIdentity, PID>, bool> predicate)
        {
            var toBeRemoved = _cacheDict.Where(predicate).ToList();
            if (toBeRemoved.Count == 0) return 0;

            var removed = 0;

            foreach (var item in toBeRemoved)
            {
                item.Key.CachedPid = null;
                if (_cacheCollection.Remove(item)) removed++;
            }

            return removed;
        }
    }
}