// -----------------------------------------------------------------------
// <copyright file="PubSubMemberDeliveryActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class PubSubMemberDeliveryActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is ProducerBatchMessage batch)
            {
                
            }

            return Task.CompletedTask;
        }
    }
}