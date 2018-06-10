// -----------------------------------------------------------------------
//   <copyright file="FastSet.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;

namespace Proto
{
    public class FastSet<T> : IEnumerable<T>
    {
        private T _first;
        private HashSet<T> _set;

        public int Count
        {
            get
            {
                if (_set == null && _first == null)
                {
                    return 0;
                }
                return _set?.Count ?? 1;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_first == null)
            {
                yield break;
            }
            if (_set == null)
            {
                yield return _first;
                yield break;
            }
            foreach (var item in _set)
            {
                yield return item;
            }
        }

        public void Add(T item)
        {
            if (_first == null)
            {
                _first = item;
                return;
            }
            if (_set == null)
            {
                _set = new HashSet<T> {_first};
            }
            _set.Add(item);
        }

        public void Remove(T item)
        {
            if (_first == null)
            {
                return;
            }
            if (_set == null  )
            {
                if (_first.Equals(item))
                {
                    _first = default(T);
                 
                }
                return;
            }
            _set.Remove(item);
        }
    }
}
