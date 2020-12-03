// -----------------------------------------------------------------------
// <copyright file="QueryOptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Testing
{
    public class QueryOptions
    {
        public ulong WaitIndex { get; set; }
        public TimeSpan WaitTime { get; set; }
    }
}