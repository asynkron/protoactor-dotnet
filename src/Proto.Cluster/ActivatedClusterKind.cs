// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Cluster
{
    public record ActivatedClusterKind
    {
        
        private int _count;

        internal ActivatedClusterKind(string name, Props props, IMemberStrategy? strategy)
        {
            Name = name;
            Props = props.WithClusterKind(this);
            Strategy = strategy;
        }

        public string Name { get; }
        public Props Props { get; }
        public IMemberStrategy? Strategy { get; }

        internal int Inc() => Interlocked.Increment(ref _count);
        internal int Dec() => Interlocked.Decrement(ref _count);
    }
}