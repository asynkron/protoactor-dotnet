namespace Proto.Cluster
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public class PidCache
    {
        private readonly ConcurrentDictionary<(string kind, string identity), PID> _cacheDict;
        private readonly ICollection<KeyValuePair<(string kind, string identity), PID>> _cacheCollection;

        public PidCache()
        {
            _cacheDict = new ConcurrentDictionary<(string kind, string identity), PID>();
            _cacheCollection = _cacheDict;
        }

        public bool TryGet(string kind, string identity, out PID pid) =>
            _cacheDict.TryGetValue((kind, identity), out pid);

        public bool TryAdd(string kind, string identity, PID pid) => _cacheDict.TryAdd((kind, identity), pid);

        public bool TryUpdate(string kind, string identity, PID newPid, PID existing)
            => _cacheDict.TryUpdate((kind, identity), newPid, existing);

        public bool TryRemove(string kind, string identity, PID pid)
            => _cacheCollection.Remove(new KeyValuePair<(string kind, string identity), PID>((kind, identity), pid));

        public bool RemoveByVal(string kind, string identity, PID pid)
        {
            var key = (kind, identity);
            if (_cacheDict.TryGetValue(key, out var existingPid) && existingPid.Id == pid.Id &&
                existingPid.Address == pid.Address)
            {
                return _cacheCollection.Remove(new KeyValuePair<(string kind, string identity), PID>(key, existingPid));
            }

            return false;
        }

        public void RemoveByMember(Member member)
        {
            RemoveByPredicate(pair => member.Address.Equals(pair.Value.Address));
        }

        private void RemoveByPredicate(Func<KeyValuePair<(string kind, string identity), PID>, bool> predicate)
        {
            var toBeRemoved = _cacheDict.Where(predicate).ToList();
            if (toBeRemoved.Count == 0) return;
            foreach (var item in toBeRemoved)
            {
                _cacheCollection.Remove(item);
            }
        }
    }
}