using System.Collections.Concurrent;
using System.Linq;

namespace Proto.Cluster.Utils
{
    public class ConcurrentSet<T>
    {
        private readonly ConcurrentDictionary<T, byte> _inner = new ConcurrentDictionary<T, byte>();

        public bool Contains(T key) => _inner.ContainsKey(key);

        public void Add(T key)
        {
            _inner.TryAdd(key, 1);
        }
        
        public bool TryAdd(T key)
        {
            return _inner.TryAdd(key, 1);
        }

        public void Remove(T key)
        {
            _inner.TryRemove(key, out _);
        }

        public T[] ToArray() => _inner.Keys.ToArray();
    }
}