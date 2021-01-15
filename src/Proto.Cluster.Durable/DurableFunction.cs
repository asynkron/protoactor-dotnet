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

        Task IActor.ReceiveAsync(IContext context)
        {
            if (context.Message is ClusterInit init)
            {
                return OnStarted(context, init);
            }
            
            if (_durableContext != null && context.Sender != null)
            {
                return OnCall(context);
            }

            return Task.CompletedTask;
        }

        private async Task OnCall(IContext context)
        {
            //if workflow not exists, save new workflow, also save message

            await _durableContext.PersistFunctionAsync(context);

            await Run(_durableContext);
        }

        private Task OnStarted(IContext context, ClusterInit init)
        {
            _identity = init.ClusterIdentity;
            _durableContext = new DurableContext(_identity, context.DurableFunctions());
            return Task.CompletedTask;
        }

        protected abstract Task Run(DurableContext context);
    }
}