// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class TopicActor : IActor
    {
        private ImmutableHashSet<string> _subscribers = ImmutableHashSet<string>.Empty;
        public Task ReceiveAsync(IContext context)
        {
            throw new NotImplementedException();
        }
    }
}