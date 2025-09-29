# Pub Sub Context

## Overview
Distributed publish/subscribe infrastructure for Proto.Cluster.

_Parent context: [Proto Cluster](../context.md)_

## Key Files
* `BatchingProducer.cs` – C# source defining Batching Producer behavior.
* `BatchingProducerConfig.cs` – C# source defining Batching Producer Config behavior.
* `DeliverBatchRequest.cs` – C# source defining Deliver Batch Request behavior.
* `IPublisher.cs` – C# source defining I Publisher behavior.
* `PubSubAutoRespondBatch.cs` – C# source defining Pub Sub Auto Respond Batch behavior.
* `PubSubBatch.cs` – C# source defining Pub Sub Batch behavior.
* `PubSubConfig.cs` – C# source defining Pub Sub Config behavior.
* `PubSubDeliveryException.cs` – C# source defining Pub Sub Delivery Exception behavior.
* `PubSubExtension.cs` – C# source defining Pub Sub Extension behavior.
* `PubSubExtensions.cs` – C# source defining Pub Sub Extensions behavior.
* `PubSubMemberDeliveryActor.cs` – C# source defining Pub Sub Member Delivery Actor behavior.
* `Publisher.cs` – C# source defining Publisher behavior.
* `PublisherConfig.cs` – C# source defining Publisher Config behavior.
* `PublishingErrorDecision.cs` – C# source defining Publishing Error Decision behavior.
* `TopicActor.cs` – C# source defining Topic Actor behavior.

## Primary Types and Contracts
* `Class BatchingProducer` defined in `BatchingProducer.cs`
* `Record ProduceMessage` defined in `BatchingProducer.cs`
* `Class PubSubBatchWithReceipts` defined in `BatchingProducer.cs`
* `Class ProducerQueueFullException` defined in `BatchingProducer.cs`
* `Record BatchingProducerConfig` defined in `BatchingProducerConfig.cs`
* `Record DeliverBatchRequest` defined in `DeliverBatchRequest.cs`
* `Class DeliverBatchRequestTransport` defined in `DeliverBatchRequest.cs`
* `Interface IPublisher` defined in `IPublisher.cs`
* `Record PubSubAutoRespondBatch` defined in `PubSubAutoRespondBatch.cs`
* `Class PubSubAutoRespondBatchTransport` defined in `PubSubAutoRespondBatch.cs`
* `Class PubSubBatch` defined in `PubSubBatch.cs`
* `Class PubSubBatchTransport` defined in `PubSubBatch.cs`
* `Record PubSubConfig` defined in `PubSubConfig.cs`
* `Class PubSubDeliveryException` defined in `PubSubDeliveryException.cs`
* `Class PubSubExtension` defined in `PubSubExtension.cs`
* `Class PubSubExtensions` defined in `PubSubExtensions.cs`
* `Class PubSubMemberDeliveryActor` defined in `PubSubMemberDeliveryActor.cs`
* `Class Publisher` defined in `Publisher.cs`
* `Class PublisherExtensions` defined in `Publisher.cs`
* `Record PublisherConfig` defined in `PublisherConfig.cs`
* `Class PublishingErrorDecision` defined in `PublishingErrorDecision.cs`
* `Class TopicActor` defined in `TopicActor.cs`

## Related Subcontexts
* No nested subcontexts.

