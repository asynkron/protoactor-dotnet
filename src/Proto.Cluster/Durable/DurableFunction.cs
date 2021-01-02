// -----------------------------------------------------------------------
// <copyright file="DurableFunction.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster.Durable
{
    [PublicAPI]
    public abstract class DurableFunction : IActor
    {
        private ClusterIdentity? _identity;
        private DurableContext? _durableContext;

        async Task IActor.ReceiveAsync(IContext context)
        {
            if (context.Message is ClusterInit init)
            {
                _identity = init.ClusterIdentity;
                _durableContext = new DurableContext(init.Cluster, _identity);
            }

            if (_durableContext != null && context.Sender != null)
            {
                //if workflow not exists, save new workflow, also save message
                
                context.Respond(123); //this should be a real message like "FunctionStarted" or something
                
                _durableContext.Message = context.Message!; //use the saved message here
                _durableContext.Counter = 0;
                await Run(_durableContext);
            }
        }

        protected abstract Task Run(DurableContext context);
    }
}