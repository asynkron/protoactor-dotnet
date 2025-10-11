# Partition Context

## Overview
Partitioning strategies and activators that distribute grain ownership across cluster members.

_Parent context: [Proto Cluster](../context.md)_

## Key Files
* `Extensions.cs` – C# source defining Extensions behavior.
* `HandoverSink.cs` – C# source defining Handover Sink behavior.
* `HandoverSource.cs` – C# source defining Handover Source behavior.
* `IndexSet.cs` – C# source defining Index Set behavior.
* `MemberHashRing.cs` – C# source defining Member Hash Ring behavior.
* `PartitionActivationLifecycle.cs` – C# source defining Partition Activation Lifecycle behavior.
* `PartitionConfig.cs` – C# source defining Partition Config behavior.
* `PartitionIdentityActor.cs` – C# source defining Partition Identity Actor behavior.
* `PartitionIdentityLookup.cs` – C# source defining Partition Identity Lookup behavior.
* `PartitionIdentityRebalanceWorker.cs` – C# source defining Partition Identity Rebalance Worker behavior.
* `PartitionManager.cs` – C# source defining Partition Manager behavior.
* `PartitionMemberSelector.cs` – C# source defining Partition Member Selector behavior.
* `PartitionPlacementActor.cs` – C# source defining Partition Placement Actor behavior.
* `PartitionPlacementRebalance.cs` – C# source defining Partition Placement Rebalance behavior.
* `Rendezvous.cs` – C# source defining Rendezvous behavior.

## Primary Types and Contracts
* `Class Extensions` defined in `Extensions.cs`
* `Class HandoverSink` defined in `HandoverSink.cs`
* `Record MemberHandoverStats` defined in `HandoverSink.cs`
* `Class MemberHandoverSink` defined in `HandoverSink.cs`
* `Class HandoverSource` defined in `HandoverSource.cs`
* `Class MemberHandover` defined in `HandoverSource.cs`
* `Class IndexSet` defined in `IndexSet.cs`
* `Class MemberHashRing` defined in `MemberHashRing.cs`
* `Class PartitionActivationLifecycle` defined in `PartitionActivationLifecycle.cs`
* `Record PartitionConfig` defined in `PartitionConfig.cs`
* `Class PartitionIdentityActor` defined in `PartitionIdentityActor.cs`
* `Class MemberStatistics` defined in `PartitionIdentityActor.cs`
* `Class MemberDetails` defined in `PartitionIdentityActor.cs`
* `Enum OperatingState` defined in `PartitionIdentityActor.cs`
* `Record PartitionCompleted` defined in `PartitionIdentityActor.cs`
* `Record PartitionFailed` defined in `PartitionIdentityActor.cs`
* `Class PartitionIdentityLookup` defined in `PartitionIdentityLookup.cs`
* `Enum Mode` defined in `PartitionIdentityLookup.cs`
* `Enum Send` defined in `PartitionIdentityLookup.cs`
* `Class PartitionIdentityRebalanceWorker` defined in `PartitionIdentityRebalanceWorker.cs`
* `Class PartitionWorker` defined in `PartitionIdentityRebalanceWorker.cs`
* `Class PartitionManager` defined in `PartitionManager.cs`
* `Class PartitionMemberSelector` defined in `PartitionMemberSelector.cs`
* `Record State` defined in `PartitionMemberSelector.cs`
* `Class PartitionPlacementActor` defined in `PartitionPlacementActor.cs`
* `Class PartitionPlacementRebalance` defined in `PartitionPlacementRebalance.cs`
* `Class MemberHandover` defined in `PartitionPlacementRebalance.cs`
* `Class Rendezvous` defined in `Rendezvous.cs`
* `Struct MemberData` defined in `Rendezvous.cs`

## Related Subcontexts
* No nested subcontexts.

