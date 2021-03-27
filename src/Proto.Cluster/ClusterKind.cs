// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;

namespace Proto.Cluster
{
    public record ClusterKind(string Name, Props Props, IMemberStrategy Strategy)
    {
        private int _count;

        internal int Inc() => Interlocked.Increment(ref _count);
        internal int Dec() => Interlocked.Decrement(ref _count);
    }
}