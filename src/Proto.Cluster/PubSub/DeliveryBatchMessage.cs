// -----------------------------------------------------------------------
// <copyright file="SubscriberBatchMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Remote;

namespace Proto.Cluster.PubSub;

public record DeliveryBatchMessage(Subscribers Subscribers, PublisherBatchMessage PublisherBatch) : IRootSerializable
{
    public IRootSerialized Serialize(ActorSystem system) => new DeliverBatchRequest
    {
        Subscribers = Subscribers,
        Batch = (ProducerBatch)PublisherBatch.Serialize(system),
    };
}

public partial class DeliverBatchRequest : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system) => 
        new DeliveryBatchMessage(Subscribers, (PublisherBatchMessage) Batch.Deserialize(system));
}