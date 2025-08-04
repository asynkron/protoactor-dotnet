# Cluster Membership Gossip Flow

This document explains how cluster membership events are detected and how
membership state is propagated through gossip.

## Detecting members

Cluster providers (e.g., Kubernetes) watch the environment for running nodes and
invoke `MemberList.UpdateClusterTopology` with the current set of members ([KubernetesClusterMonitor.cs](src/Proto.Cluster.Kubernetes/KubernetesClusterMonitor.cs#L304-L317)).
`UpdateClusterTopology` filters out blocked members, computes which members
joined or left, and constructs a `ClusterTopology` that includes `Joined`,
`Left`, and `Blocked` lists ([MemberList.cs](src/Proto.Cluster/Member/MemberList.cs#L161-L240)).
The resulting topology is published to the node's local event stream via
`BroadcastTopologyChanges` ([MemberList.cs](src/Proto.Cluster/Member/MemberList.cs#L349-L353)).

## Propagating membership to gossip

The `Gossiper` subscribes to `ClusterTopology` events. Each update is cloned with
its `Joined` and `Left` lists cleared before being forwarded to the `GossipActor`
for inclusion in the gossip state ([Gossiper.cs](src/Proto.Cluster/Gossip/Gossiper.cs#L190-L204)).
The gossip implementation stores the full membership under the `cluster:topology`
key and tracks active member IDs for later consensus checks ([Gossip.cs](src/Proto.Cluster/Gossip/Gossip.cs#L85-L92)).

## Gossip dissemination

The gossip loop periodically updates heartbeat information and sends the current
state to randomly chosen peers ([Gossiper.cs](src/Proto.Cluster/Gossip/Gossiper.cs#L232-L241), [Gossip.cs](src/Proto.Cluster/Gossip/Gossip.cs#L168-L205)).
Peers merge received updates, which allows membership changes to spread
throughout the cluster.

## Member states

- **Joined / Left** – Calculated by `MemberList.UpdateClusterTopology` and
included in the published topology ([MemberList.cs](src/Proto.Cluster/Member/MemberList.cs#L197-L238)).
- **Gracefully left** – When a node shuts down gracefully it sets the
`cluster:left` gossip key, waits two gossip intervals, and deregisters from the
provider ([Cluster.cs](src/Proto.Cluster/Cluster.cs#L286-L299)). Other nodes read this key and
block those members ([Gossiper.cs](src/Proto.Cluster/Gossip/Gossiper.cs#L262-L277)).
- **Blocked** – Members are blocked when they leave or when their heartbeat
expires. Heartbeat data is stored under `cluster:heartbeat`, and expired
entries cause nodes to be added to the block list ([Gossiper.cs](src/Proto.Cluster/Gossip/Gossiper.cs#L281-L310)).

The combination of provider detection, event publication, and gossip
propagation ensures that membership changes reach all cluster nodes without
cluster-wide broadcasts.
