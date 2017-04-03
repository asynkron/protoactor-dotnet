// -----------------------------------------------------------------------
//   <copyright file="FastSet.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;

namespace Proto
{
    internal class FastSet<T> : IEnumerable<T>
    {
        private T _first;
        private HashSet<T> _set;

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
