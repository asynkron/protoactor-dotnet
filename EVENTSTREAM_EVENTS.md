# EventStream events in Proto.Remote and Proto.Cluster

This document lists key events published on Proto.Actor's `EventStream` and how they
coordinate behaviour between the **Remote** and **Cluster** modules.
For each event we show who publishes it, when it is raised, and which
components subscribe to it.

## MemberBlocked
- **Published by:** `BlockList` when new member IDs are added to the block list
  (e.g. unresponsive or leaving members) ([src/Proto.Remote/BlockList.cs](src/Proto.Remote/BlockList.cs#L42-L56))
- **Subscribed by:** `MemberList` to log the block and recompute topology.
  If the local node is blocked it invokes `SelfBlocked` ([src/Proto.Cluster/Membership/MemberList.cs](src/Proto.Cluster/Membership/MemberList.cs#L86-L114))

## EndpointTerminatedEvent
- **Published by:**
  - `ServerConnector` when a remote connection is refused or fails ([src/Proto.Remote/Endpoints/ServerConnector.cs](src/Proto.Remote/Endpoints/ServerConnector.cs#L141-L166) and [src/Proto.Remote/Endpoints/ServerConnector.cs](src/Proto.Remote/Endpoints/ServerConnector.cs#L199-L229))
  - `EndpointReader` when a channel closes ([src/Proto.Remote/Endpoints/EndpointReader.cs](src/Proto.Remote/Endpoints/EndpointReader.cs#L238-L241))
  - `MemberList` when a cluster member leaves ([src/Proto.Cluster/Membership/MemberList.cs](src/Proto.Cluster/Membership/MemberList.cs#L355-L365))
- **Subscribed by:** `EndpointManager` which disposes the endpoint and optionally
  blocks the address or system ID ([src/Proto.Remote/Endpoints/EndpointManager.cs](src/Proto.Remote/Endpoints/EndpointManager.cs#L25-L44) and [src/Proto.Remote/Endpoints/EndpointManager.cs](src/Proto.Remote/Endpoints/EndpointManager.cs#L91-L110))

## ClusterTopology
- **Published by:** `MemberList.UpdateClusterTopology` whenever membership changes
  (join/leave/block) and broadcast to all members ([src/Proto.Cluster/Membership/MemberList.cs](src/Proto.Cluster/Membership/MemberList.cs#L160-L232) and [src/Proto.Cluster/Membership/MemberList.cs](src/Proto.Cluster/Membership/MemberList.cs#L349-L353))
- **Subscribed by:** many cluster components, for example:
  - `Cluster` removes cached PIDs for members that left ([src/Proto.Cluster/Cluster.cs](src/Proto.Cluster/Cluster.cs#L146-L153))
  - `PartitionManager` updates placement selectors and forwards the update to
    partition actors ([src/Proto.Cluster/Partition/PartitionManager.cs](src/Proto.Cluster/Partition/PartitionManager.cs#L43-L82))
  - `IdentityStorageLookup` cleans storage for departing members ([src/Proto.Cluster/Identity/IdentityStorageLookup.cs](src/Proto.Cluster/Identity/IdentityStorageLookup.cs#L56-L63))

## ActivationTerminating
- **Published by:** `ClusterExtension` when a cluster-identity actor is stopping ([src/Proto.Cluster/ClusterExtension.cs](src/Proto.Cluster/ClusterExtension.cs#L184-L194))
- **Subscribed by:** placement/activation actors to remove local state and
  broadcast termination:
  - `PartitionActivatorActor` subscribes on start ([src/Proto.Cluster/PartitionActivator/PartitionActivatorActor.cs](src/Proto.Cluster/PartitionActivator/PartitionActivatorActor.cs#L63-L68))
  - `PartitionPlacementActor` subscribes and forwards the event to itself ([src/Proto.Cluster/Partition/PartitionPlacementActor.cs](src/Proto.Cluster/Partition/PartitionPlacementActor.cs#L190-L194))
  - `IdentityStoragePlacementActor` subscribes for clean-up ([src/Proto.Cluster/Identity/IdentityStoragePlacementActor.cs](src/Proto.Cluster/Identity/IdentityStoragePlacementActor.cs#L52-L56))

## ActivationTerminated
- **Published by:** actors handling `ActivationTerminating`; e.g.
  `PartitionActivatorActor` broadcasts the termination to all nodes so caches
  can be cleared ([src/Proto.Cluster/PartitionActivator/PartitionActivatorActor.cs](src/Proto.Cluster/PartitionActivator/PartitionActivatorActor.cs#L137-L147))
- **Subscribed by:** components maintaining activation indexes/caches, such as
  `PartitionIdentityActor`, which removes the PID and clears the cache on receipt ([src/Proto.Cluster/Partition/PartitionIdentityActor.cs](src/Proto.Cluster/Partition/PartitionIdentityActor.cs#L226-L230) and [src/Proto.Cluster/Partition/PartitionIdentityActor.cs](src/Proto.Cluster/Partition/PartitionIdentityActor.cs#L501-L522))

## GossipUpdate
- **Published by:** `GossipActor` for each gossip state update received from a
  peer ([src/Proto.Cluster/Gossip/GossipActor.cs](src/Proto.Cluster/Gossip/GossipActor.cs#L162-L169))
- **Subscribed by:**
  - `MemberList` to merge block lists learned via gossip ([src/Proto.Cluster/Membership/MemberList.cs](src/Proto.Cluster/Membership/MemberList.cs#L86-L98))
  - `GossipMemberStrategy` to track member heartbeats and actor counts ([src/Proto.Cluster/Membership/GossipMemberStrategy.cs](src/Proto.Cluster/Membership/GossipMemberStrategy.cs#L29-L45))

## DeadLetterEvent
- **Published by:** remote endpoints when a message cannot be delivered, either
  by the endpoint itself or when targeting a blocked address ([src/Proto.Remote/Endpoints/Endpoint.cs](src/Proto.Remote/Endpoints/Endpoint.cs#L296-L305), [src/Proto.Remote/Endpoints/Endpoint.cs](src/Proto.Remote/Endpoints/Endpoint.cs#L364-L370) and [src/Proto.Remote/Endpoints/BlockedEndpoint.cs](src/Proto.Remote/Endpoints/BlockedEndpoint.cs#L56-L61))
- **Subscribed by:** optional diagnostics subscribers; no core cluster/remote
  component subscribes by default.

These events provide loose coupling between modules, allowing remote and cluster
infrastructure to react to membership changes, connection failures, and
activation lifecycle events.
