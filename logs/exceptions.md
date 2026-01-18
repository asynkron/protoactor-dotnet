### Proto.Cluster.Tests
`System.Exception: Failed to reach consensus` observed during cluster initialization in multiple tests (e.g., RedundantGossipTests.GossipRequest_is_sent_multiple_times_without_state_changes).

### Proto.Cluster.Tests (NullReference)
`System.NullReferenceException: Object reference not set to an instance of an object.` at `Gossiper.StartGossipActorAsync` during multiple tests in `Proto.Cluster.Tests`.

### Proto.Cluster.Tests (Timeout)
`System.TimeoutException: Request timed out` in `InMemoryPartitionActivatorClusterTests.HandlesSlowResponsesCorrectly`.

### Proto.Actor.Tests.EscalateFailureTests
`System.TimeoutException: The condition was not met within the timeout of 00:00:00.1000000` in `Proto.Mailbox.Tests.EscalateFailureTests.GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure`.

### Proto.Mailbox.Tests.EscalateFailureTests.GivenNonCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure
`System.TimeoutException: The condition was not met within the timeout of 00:00:00.1000000` when waiting for mailbox failure escalation.

### Proto.Tests.SupervisionTestsAllForOne.AllForOneStrategy_Should_PassExceptionOnRestart
`System.InvalidOperationException: Collection was modified; enumeration operation may not execute.` during enumeration of mailbox statistics.

### Proto.Cluster.Tests.GossipCoreTests.Large_cluster_should_get_topology_consensus
`Test failure: output indicated [FAIL] but passed on rerun; no stack trace captured.`

### Proto.Tests.SupervisionTestsAlwaysRestart.AlwaysRestartStrategy_Should_RestartFailingChildOnly
`Assert.Equal() Failure: Values differ. Expected: 1 Actual: 0` in `SupervisionTests_AlwaysRestart.cs:line 43`.

### Proto.Cluster.Tests.ClusterTopologyBuilderTests.Compute_FiltersBlockedAndDuplicates
`Assert.Equal() Failure: Strings differ Expected: "4" Actual: "3"`

### Proto.Cluster.Tests.GossipCoreTests.Large_cluster_should_get_topology_consensus
`Expected x.consensus to be True, but found False.`

### Proto.Tests.SupervisionTestsOneForOne.OneForOneStrategy_Should_EscalateFailureToParent
`System.InvalidOperationException: Sequence contains no elements` in `SupervisionTests_OneForOne.cs:line 263`.
### Proto.Mailbox.Tests.MailboxSchedulingTests.GivenNonCompletedUserMessage_ShouldHaltProcessingUntilCompletion
`System.TimeoutException: The condition was not met within the timeout of 00:00:00.1000000`

### Proto.Tests.ActorTests.StopActorWithLongRunningTask
`Proto.TestKit.TestKitException: Expected user message of type System.Threading.Tasks.TaskCanceledException, but received system message of type Proto.Stopping`

### Proto.Tests.ReceiveTimeoutTests.receive_timeout_is_reset_by_influencing_messages
`Proto.TestKit.TestKitException : Waited 1 seconds but failed to receive a message` observed when ReceiveTimeout was not delivered after cancelling scheduled ticks.

### Proto.Cluster.Tests.RedundantGossipTests.GossipRequest_is_sent_multiple_times_without_state_changes
`Expected fixture.SerializedKeyCount to be 3, but found 1.`

### Proto.Cluster.Tests.GossipCoreTests.Large_cluster_should_get_topology_consensus
`Expected x.consensus to be True, but found False.`

### EndpointReader.RunReader
`System.InvalidOperationException: Can't read messages after the request is complete.` observed when a node leaves the cluster.

### Proto.Tests.ReceiveTimeoutTests.receive_timeout_received_within_expected_time_when_sending_ignored_messages
```
Proto.TestKit.TestKitException : Waited 1 seconds but failed to receive a message
```

### Proto.Tests.SupervisionTestsAlwaysRestart.AlwaysRestartStrategy_Should_RestartFailingChildOnly
`Assert.Equal() Failure: Values differ. Expected: 1 Actual: 0` encountered during initial `dotnet test` run; rerun passed.

### Proto.Cluster.Tests.GossipCoreTests.Large_cluster_should_get_topology_consensus
`Expected x.consensus to be True, but found False.` occurred twice before extending the consensus timeout to 20 seconds.

### Proto.Tests.SharedFutureTests.Should_wrap_request_ids_without_hitting_zero
`Expected future.Pid.RequestId to be 8u, but found 1u.` observed when verifying request id wrap-around on .NET 8 after the upgrade; focused rerun and a full-suite retry passed.

### Issue2151 KubernetesProvider.DeregisterMemberInner
```
System.ArgumentNullException: Value cannot be null. (Parameter 'source')
   at System.Linq.ThrowHelper.ThrowArgumentNullException(ExceptionArgument argument)
   at System.Linq.Enumerable.Where[TSource](IEnumerable`1 source, Func`2 predicate)
   at Proto.Cluster.Kubernetes.KubernetesProvider.DeregisterMemberInner(Cluster cluster)
```
