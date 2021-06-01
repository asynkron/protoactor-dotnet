// -----------------------------------------------------------------------
// <copyright file="SubscriberBatchMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Proto.Remote;

namespace Proto.Cluster
{
    public record DeliveryBatchMessage(Subscribers subscribers, ProducerBatchMessage ProducerBatch) : IRootSerializable
    {
        public IRootSerialized Serialize(ActorSystem system) => new DeliveryBatch
        {
            Subscribers = subscribers,
            Batch = (ProducerBatch)ProducerBatch.Serialize(system),
        };
    }

    public partial class DeliveryBatch: IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system) => 
            new DeliveryBatchMessage(Subscribers, (ProducerBatchMessage) Batch.Deserialize(system));
    }
}