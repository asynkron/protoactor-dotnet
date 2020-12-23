// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Proto.Metrics
{
    internal static class Extensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? self) => self ?? Array.Empty<T>();
        
        public static void ForEach<T>(this IEnumerable<T>? self, Action<T> action)
        {
            foreach (var item in self)
            {
                action(item);
            }
        }
    }
}