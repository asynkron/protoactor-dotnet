### Proto.Cluster.Tests
`System.Exception: Failed to reach consensus` observed during cluster initialization in multiple tests (e.g., RedundantGossipTests.GossipRequest_is_sent_multiple_times_without_state_changes).

### Proto.Cluster.Tests (NullReference)
`System.NullReferenceException: Object reference not set to an instance of an object.` at `Gossiper.StartGossipActorAsync` during multiple tests in `Proto.Cluster.Tests`.

### Proto.Cluster.Tests (Timeout)
`System.TimeoutException: Request timed out` in `InMemoryPartitionActivatorClusterTests.HandlesSlowResponsesCorrectly`.
