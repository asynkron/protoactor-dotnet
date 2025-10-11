# Membership Context

## Overview
Cluster membership model, events, providers, and observers.

_Parent context: [Proto Cluster](../context.md)_

## Key Files
* `ClusterTopologyBuilder.cs` – C# source defining Cluster Topology Builder behavior.
* `ConsensusManager.cs` – C# source defining Consensus Manager behavior.
* `GossipMemberStrategy.cs` – C# source defining Gossip Member Strategy behavior.
* `IMemberList.cs` – C# source defining I Member List behavior.
* `ImmutableMemberSet.cs` – C# source defining Immutable Member Set behavior.
* `LeaderElection.cs` – C# source defining Leader Election behavior.
* `LocalAffinityExtensions.cs` – C# source defining Local Affinity Extensions behavior.
* `LocalAffinityOptions.cs` – C# source defining Local Affinity Options behavior.
* `LocalAffinityStrategy.cs` – C# source defining Local Affinity Strategy behavior.
* `MemberList.cs` – C# source defining Member List behavior.
* `MemberListLogMessages.cs` – C# source defining Member List Log Messages behavior.
* `MemberStrategy.cs` – C# source defining Member Strategy behavior.
* `MemberStrategyManager.cs` – C# source defining Member Strategy Manager behavior.
* `MetaMember.cs` – C# source defining Meta Member behavior.
* `RoundRobinMemberSelector.cs` – C# source defining Round Robin Member Selector behavior.

## Primary Types and Contracts
* `Record TopologyChanges` defined in `ClusterTopologyBuilder.cs`
* `Class ClusterTopologyBuilder` defined in `ClusterTopologyBuilder.cs`
* `Class ConsensusManager` defined in `ConsensusManager.cs`
* `Class GossipMemberStrategy` defined in `GossipMemberStrategy.cs`
* `Interface IMemberList` defined in `IMemberList.cs`
* `Class ImmutableMemberSet` defined in `ImmutableMemberSet.cs`
* `Class LeaderElection` defined in `LeaderElection.cs`
* `Record LeaderElected` defined in `LeaderElection.cs`
* `Class LocalAffinityExtensions` defined in `LocalAffinityExtensions.cs`
* `Class Tombstone` defined in `LocalAffinityExtensions.cs`
* `Record LocalAffinityOptions` defined in `LocalAffinityOptions.cs`
* `Class LocalAffinityStrategy` defined in `LocalAffinityStrategy.cs`
* `Record MemberList` defined in `MemberList.cs`
* `Class MemberListLogMessages` defined in `MemberListLogMessages.cs`
* `Interface IMemberStrategy` defined in `MemberStrategy.cs`
* `Class SimpleMemberStrategy` defined in `MemberStrategy.cs`
* `Class MemberStrategyManager` defined in `MemberStrategyManager.cs`
* `Record MetaMember` defined in `MetaMember.cs`
* `Class RoundRobinMemberSelector` defined in `RoundRobinMemberSelector.cs`

## Related Subcontexts
* No nested subcontexts.

