// -----------------------------------------------------------------------
// <copyright file="DurableOrchestrator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Cluster.Durable
{
    public class DurableOrchestrator : IActor
    {
        public Task ReceiveAsync(IContext context) => throw new NotImplementedException();
    }
}