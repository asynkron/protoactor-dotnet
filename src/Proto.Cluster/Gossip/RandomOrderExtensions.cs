// -----------------------------------------------------------------------
// <copyright file="RandomOrderExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.Gossip
{
    public static class RandomOrderExtensions
    {
        public static  IEnumerable<T> OrderByRandom<T>(this IEnumerable<T> items, Random rnd) =>
            items
                .Select(m => (item: m, index: rnd.Next()))
                .OrderBy(m => m.index)
                .Select(m => m.item);
        
        
        public static  IEnumerable<T> OrderByRandom<T>(this IEnumerable<T> items, Random rnd, Func<T,bool> shouldBeFirst) =>
            items
                .Select(m => (item: m, index: rnd.Next()))
                .OrderBy(m => {
                        if (shouldBeFirst(m.item))
                        {
                            return -1;
                        }

                        return m.index;
                    }
                )
                .Select(m => m.item);
    }
}