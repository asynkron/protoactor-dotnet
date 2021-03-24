// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Cluster
{
    public record ClusterKind(string name, Props props)
    {
        private int _count;

        public void Inc() => Interlocked.Increment(ref _count);
        public void Dec() => Interlocked.Decrement(ref _count);
    }
}