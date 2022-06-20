// -----------------------------------------------------------------------
// <copyright file="SubscriberBatchMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Remote;

namespace Proto.Cluster.PubSub;

public record DeliverBatchRequest(Subscribers Subscribers, PubSubBatch PubSubBatch) : IRootSerializable
{
    public IRootSerialized Serialize(ActorSystem system) => new DeliverBatchRequestTransport
    {
        Subscribers = Subscribers,
        Batch = (PubSubBatchTransport) PubSubBatch.Serialize(system)
    };
}

public partial class DeliverBatchRequestTransport : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system) =>
        new DeliverBatchRequest(Subscribers, (PubSubBatch) Batch.Deserialize(system));
}