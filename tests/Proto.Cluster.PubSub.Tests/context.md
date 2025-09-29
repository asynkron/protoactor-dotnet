# Proto Cluster Pub Sub Tests Context

## Overview
Dedicated tests for the cluster pub-sub subsystem.

_Parent context: [Tests](../context.md)_

## Key Files
* `InMemorySubscribersStore.cs` – C# source defining In Memory Subscribers Store behavior.
* `Proto.Cluster.PubSub.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `PubSubBatchingProducerTests.cs` – C# source defining Pub Sub Batching Producer Tests behavior.
* `PubSubClientTests.cs` – C# source defining Pub Sub Client Tests behavior.
* `PubSubClusterFixture.cs` – C# source defining Pub Sub Cluster Fixture behavior.
* `PubSubDefaultTopicRegistrationTests.cs` – C# source defining Pub Sub Default Topic Registration Tests behavior.
* `PubSubMemberTests.cs` – C# source defining Pub Sub Member Tests behavior.
* `PubSubTests.cs` – C# source defining Pub Sub Tests behavior.

## Primary Types and Contracts
* `Class InMemorySubscribersStore` defined in `InMemorySubscribersStore.cs`
* `Class PubSubBatchingProducerTests` defined in `PubSubBatchingProducerTests.cs`
* `Class MockPublisher` defined in `PubSubBatchingProducerTests.cs`
* `Class OptionalFailureMockPublisher` defined in `PubSubBatchingProducerTests.cs`
* `Record TestMessage` defined in `PubSubBatchingProducerTests.cs`
* `Class TestException` defined in `PubSubBatchingProducerTests.cs`
* `Class PubSubClientTests` defined in `PubSubClientTests.cs`
* `Record DataPublished` defined in `PubSubClusterFixture.cs`
* `Record Delivery` defined in `PubSubClusterFixture.cs`
* `Record Response` defined in `PubSubClusterFixture.cs`
* `Class PubSubClusterFixture` defined in `PubSubClusterFixture.cs`
* `Class PubSubDefaultTopicRegistrationTests` defined in `PubSubDefaultTopicRegistrationTests.cs`
* `Class PubSubMemberTests` defined in `PubSubMemberTests.cs`
* `Class PubSubTests` defined in `PubSubTests.cs`

## Related Subcontexts
* No nested subcontexts.

