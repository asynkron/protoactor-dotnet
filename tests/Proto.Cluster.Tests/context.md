# Proto Cluster Tests Context

## Overview
Cluster runtime tests including membership, identity, partitioning, and pub-sub behaviors.

_Parent context: [Tests](../context.md)_

## Key Files
* `AssemblyInfo.cs` – C# source defining Assembly Info behavior.
* `ClusterFixture.cs` – C# source defining Cluster Fixture behavior.
* `ClusterTestBase.cs` – C# source defining Cluster Test Base behavior.
* `ClusterTests.cs` – C# source defining Cluster Tests behavior.
* `ClusterTestsWithLocalAffinity.cs` – C# source defining Cluster Tests With Local Affinity behavior.
* `ClusterTopologyBuilderTests.cs` – C# source defining Cluster Topology Builder Tests behavior.
* `ConcurrencyVerificationActor.cs` – C# source defining Concurrency Verification Actor behavior.
* `ConsensusEvaluatorTests.cs` – C# source defining Consensus Evaluator Tests behavior.
* `ConsulTests.cs` – C# source defining Consul Tests behavior.
* `DeterministicRandomProvider.cs` – C# source defining Deterministic Random Provider behavior.
* `EchoActor.cs` – C# source defining Echo Actor behavior.
* `ExpectUpdatedTopologyConsensusTests.cs` – C# source defining Expect Updated Topology Consensus Tests behavior.
* `Extensions.cs` – C# source defining Extensions behavior.
* `ForcedSerializationTests.cs` – C# source defining Forced Serialization Tests behavior.
* `GithubActionsReporter.cs` – C# source defining Github Actions Reporter behavior.
* `GossipConsensusTests.cs` – C# source defining Gossip Consensus Tests behavior.
* `GossipCoreTests.cs` – C# source defining Gossip Core Tests behavior; the large-cluster consensus test now allows a longer gossip window to reduce flakiness.
* `GossipDisseminationTests.cs` – C# source defining Gossip Dissemination Tests behavior.
* `GossipRandomOrderingTests.cs` – C# source defining Gossip Random Ordering Tests behavior.
* `GossipRequestValidationTests.cs` – C# source defining Gossip Request Validation Tests behavior.
* `GossipStateManagementTests.cs` – C# source defining Gossip State Management Tests behavior.
* `GossipStateQueryTests.cs` – C# source defining Gossip State Query Tests behavior.
* `GossipTests.cs` – C# source defining Gossip Tests behavior.
* `GossipTransportTests.cs` – C# source defining Gossip Transport Tests behavior.
* `GracefulLeaveTests.cs` – C# source defining Graceful Leave Tests behavior.
* `InMemorySubscribersStore.cs` – C# source defining In Memory Subscribers Store behavior.
* `LegacyTimeoutTests.cs` – C# source defining Legacy Timeout Tests behavior.
* `MemberStateDeltaBuilderTests.cs` – C# source defining Member State Delta Builder Tests behavior.
* `OrderedDeliveryTests.cs` – C# source defining Ordered Delivery Tests behavior.
* `PartitionConsensusTests.cs` – C# source defining Partition Consensus Tests behavior.
* `PartitionMiddlewareTests.cs` – C# source defining Partition Middleware Tests behavior.
* `PidCacheInvalidationTests.cs` – C# source defining Pid Cache Invalidation Tests behavior.
* `PidCacheTests.cs` – C# source defining Pid Cache Tests behavior.
* `Proto.Cluster.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `RedundantGossipTests.cs` – C# source defining Redundant Gossip Tests behavior.
* `RemoteGossipTests.cs` – C# source defining Remote Gossip Tests behavior.
* `RetryOnDeadLetterTests.cs` – C# source defining Retry On Dead Letter Tests behavior.
* `TimeoutTests.cs` – C# source defining Timeout Tests behavior.
* `UnreachableSubscriberTests.cs` – C# source defining Unreachable Subscriber Tests behavior.
* `docker-compose.yml` – YAML configuration.
* `messages.proto` – Protocol Buffers schema.

## Primary Types and Contracts
* `Interface IClusterFixture` defined in `ClusterFixture.cs`
* `Class TracingSettings` defined in `ClusterFixture.cs`
* `Class ClusterFixture` defined in `ClusterFixture.cs`
* `Class BaseInMemoryClusterFixture` defined in `ClusterFixture.cs`
* `Class InMemoryClusterFixture` defined in `ClusterFixture.cs`
* `Class InMemoryClusterFixtureWithPartitionActivator` defined in `ClusterFixture.cs`
* `Class InMemoryClusterFixtureAlternativeClusterContext` defined in `ClusterFixture.cs`
* `Class InMemoryClusterFixtureSharedFutures` defined in `ClusterFixture.cs`
* `Class InMemoryPidCacheInvalidationClusterFixture` defined in `ClusterFixture.cs`
* `Class SingleNodeProviderFixture` defined in `ClusterFixture.cs`
* `Class ClusterTestBase` defined in `ClusterTestBase.cs`
* `Class ClusterTests` defined in `ClusterTests.cs`
* `Class InMemoryPartitionActivatorClusterTests` defined in `ClusterTests.cs`
* `Class SingleNodeProviderClusterTests` defined in `ClusterTests.cs`
* `Class ClusterTestsWithLocalAffinity` defined in `ClusterTestsWithLocalAffinity.cs`
* `Class InMemoryClusterTests` defined in `ClusterTestsWithLocalAffinity.cs`
* `Class InMemoryClusterTestsSharedFutures` defined in `ClusterTestsWithLocalAffinity.cs`
* `Class InMemoryClusterTestsPidCacheInvalidation` defined in `ClusterTestsWithLocalAffinity.cs`
* `Class ClusterTopologyBuilderTests` defined in `ClusterTopologyBuilderTests.cs`
* `Class ConcurrencyVerificationActor` defined in `ConcurrencyVerificationActor.cs`
* `Record VerificationEvent` defined in `ConcurrencyVerificationActor.cs`
* `Record ActorStarted` defined in `ConcurrencyVerificationActor.cs`
* `Record ActorStopped` defined in `ConcurrencyVerificationActor.cs`
* `Record ActivationRequested` defined in `ConcurrencyVerificationActor.cs`
* `Record ConsistencyError` defined in `ConcurrencyVerificationActor.cs`
* `Record ClusterSnapshot` defined in `ConcurrencyVerificationActor.cs`
* `Class ActorState` defined in `ConcurrencyVerificationActor.cs`
* `Class ActorStateRepo` defined in `ConcurrencyVerificationActor.cs`
* `Class ConsensusEvaluatorTests` defined in `ConsensusEvaluatorTests.cs`
* `Class ConsulClusterFixture` defined in `ConsulTests.cs`
* `Class DeterministicRandomProvider` defined in `DeterministicRandomProvider.cs`
* `Class EchoActor` defined in `EchoActor.cs`
* `Class ExpectUpdatedTopologyConsensusTests` defined in `ExpectUpdatedTopologyConsensusTests.cs`
* `Class Extensions` defined in `Extensions.cs`
* `Class ForcedSerializationTests` defined in `ForcedSerializationTests.cs`
* `Class ForcedSerializationClusterFixture` defined in `ForcedSerializationTests.cs`
* `Class GithubActionsReporter` defined in `GithubActionsReporter.cs`
* `Record TestResult` defined in `GithubActionsReporter.cs`
* `Class GossipConsensusTests` defined in `GossipConsensusTests.cs`
* `Class GossipCoreTests` defined in `GossipCoreTests.cs`
* `Class GossipDisseminationTests` defined in `GossipDisseminationTests.cs`
* `Class GossipRandomOrderingTests` defined in `GossipRandomOrderingTests.cs`
* `Class GossipRequestValidationTests` defined in `GossipRequestValidationTests.cs`
* `Class GossipStateManagementTests` defined in `GossipStateManagementTests.cs`
* `Class GossipStateQueryTests` defined in `GossipStateQueryTests.cs`
* `Class GossipTests` defined in `GossipTests.cs`
* `Class GossipClusterFixture` defined in `GossipTests.cs`
* `Class GossipTransportTests` defined in `GossipTransportTests.cs`
* `Class MockTransport` defined in `GossipTransportTests.cs`
* `Class GracefulLeaveTests` defined in `GracefulLeaveTests.cs`
* `Class TwoMembersClusterFixture` defined in `GracefulLeaveTests.cs`
* `Class InMemorySubscribersStore` defined in `InMemorySubscribersStore.cs`
* `Class LegacyTimeoutTests` defined in `LegacyTimeoutTests.cs`
* `Class Fixture` defined in `LegacyTimeoutTests.cs`
* `Class MemberStateDeltaBuilderTests` defined in `MemberStateDeltaBuilderTests.cs`
* `Class OrderedDeliveryTests` defined in `OrderedDeliveryTests.cs`
* `Class SenderActor` defined in `OrderedDeliveryTests.cs`
* `Class VerifyOrderActor` defined in `OrderedDeliveryTests.cs`
* `Class OrderedDeliveryFixture` defined in `OrderedDeliveryTests.cs`
* `Class PartitionConsensusTests` defined in `PartitionConsensusTests.cs`
* `Class PartitionClusterFixture` defined in `PartitionConsensusTests.cs`
* `Class PartitionActivatorMiddlewareTests` defined in `PartitionMiddlewareTests.cs`
* `Class ActivatorMiddlewareFixture` defined in `PartitionMiddlewareTests.cs`
* `Class PartitionPlacementMiddlewareTests` defined in `PartitionMiddlewareTests.cs`
* `Class PlacementMiddlewareFixture` defined in `PartitionMiddlewareTests.cs`
* `Class PidCacheInvalidationTests` defined in `PidCacheInvalidationTests.cs`
* `Class DummyIdentityLookup` defined in `PidCacheTests.cs`
* `Class PidCacheTests` defined in `PidCacheTests.cs`
* `Class RedundantGossipTests` defined in `RedundantGossipTests.cs`
* `Class GossipCountingClusterFixture` defined in `RedundantGossipTests.cs`
* `Class RemoteGossipTests` defined in `RemoteGossipTests.cs`
* `Class TestMemberList` defined in `RemoteGossipTests.cs`
* `Class RetryOnDeadLetterTests` defined in `RetryOnDeadLetterTests.cs`
* `Class Fixture` defined in `RetryOnDeadLetterTests.cs`
* `Class TimeoutTests` defined in `TimeoutTests.cs`
* `Class Fixture` defined in `TimeoutTests.cs`
* `Class UnreachableSubscriberTests` defined in `UnreachableSubscriberTests.cs`
* `Record DataPublished` defined in `UnreachableSubscriberTests.cs`
* `Record Delivery` defined in `UnreachableSubscriberTests.cs`
* `Record Response` defined in `UnreachableSubscriberTests.cs`
* `Class Fixture` defined in `UnreachableSubscriberTests.cs`

## Related Subcontexts
* No nested subcontexts.
