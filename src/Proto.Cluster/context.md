# Proto Cluster Context

## Overview
Cluster runtime: membership, identity lookup, partitioning, pub-sub, gossip, metrics, and grain activation.

_Parent context: [Src](../context.md)_

## Key Files
* `ActivatedClusterKind.cs` – C# source defining Activated Cluster Kind behavior.
* `Cluster.cs` – C# source defining Cluster behavior.
* `ClusterConfig.cs` – C# source defining Cluster Config behavior.
* `ClusterContext.cs` – C# source defining Cluster Context behavior.
* `ClusterContextConfig.cs` – C# source defining Cluster Context Config behavior.
* `ClusterContracts.proto` – Protocol Buffers schema.
* `ClusterDiagnostics.cs` – C# source defining Cluster Diagnostics behavior.
* `ClusterExtension.cs` – C# source defining Cluster Extension behavior.
* `ClusterExtensions.cs` – C# source defining Cluster Extensions behavior.
* `ClusterInitialization.cs` – C# source defining Cluster Initialization behavior.
* `ClusterKind.cs` – C# source defining Cluster Kind behavior.
* `ClusterMetricsCollector.cs` – C# source defining Cluster Metrics Collector behavior.
* `DefaultClusterContext.cs` – C# source defining Default Cluster Context behavior.
* `DiagnosticsContracts.cs` – C# source defining Diagnostics Contracts behavior.
* `GossipContracts.proto` – Protocol Buffers schema.
* `GrainContracts.proto` – Protocol Buffers schema.
* `IClusterProvider.cs` – C# source defining I Cluster Provider behavior.
* `InternalsVisibleTo.cs` – C# source defining Internals Visible To behavior.
* `Member.cs` – C# source defining Member behavior.
* `PidCache.cs` – C# source defining Pid Cache behavior.
* `Proto.Cluster.csproj` – Project file configuring compilation targets and dependencies.
* `ProtoActorLifecycleHost.cs` – C# source defining Proto Actor Lifecycle Host behavior.
* `PubSubContracts.proto` – Protocol Buffers schema.
* `SeedContracts.proto` – Protocol Buffers schema.
* `ServiceCollectionExtensions.cs` – C# source defining Service Collection Extensions behavior.

## Primary Types and Contracts
* `Record ActivatedClusterKind` defined in `ActivatedClusterKind.cs`
* `Class Cluster` defined in `Cluster.cs`
* `Record ClusterConfig` defined in `ClusterConfig.cs`
* `Interface IClusterContext` defined in `ClusterContext.cs`
* `Record ClusterContextConfig` defined in `ClusterContextConfig.cs`
* `Class ClusterConfigExtensions` defined in `ClusterContextConfig.cs`
* `Class ClusterDiagnostics` defined in `ClusterDiagnostics.cs`
* `Class Extensions` defined in `ClusterExtension.cs`
* `Struct PidRef` defined in `ClusterExtension.cs`
* `Class ClusterExtensions` defined in `ClusterExtensions.cs`
* `Class ClusterInitialization` defined in `ClusterInitialization.cs`
* `Record ClusterKind` defined in `ClusterKind.cs`
* `Class ClusterMetricsCollector` defined in `ClusterMetricsCollector.cs`
* `Class DefaultClusterContext` defined in `DefaultClusterContext.cs`
* `Enum PidSource` defined in `DefaultClusterContext.cs`
* `Record DiagnosticsMemberHeartbeat` defined in `DiagnosticsContracts.cs`
* `Interface IClusterProvider` defined in `IClusterProvider.cs`
* `Class Member` defined in `Member.cs`
* `Class PidCache` defined in `PidCache.cs`
* `Class ProtoActorLifecycleHost` defined in `ProtoActorLifecycleHost.cs`
* `Class HostedClusterConfig` defined in `ServiceCollectionExtensions.cs`
* `Class ServiceCollectionExtensions` defined in `ServiceCollectionExtensions.cs`

## Related Subcontexts
* [Cache](Cache/context.md) – See the nested context for details.
* [Gossip](Gossip/context.md) – See the nested context for details.
* [Grain](Grain/context.md) – See the nested context for details.
* [Healthchecks](Healthchecks/context.md) – See the nested context for details.
* [Identity](Identity/context.md) – See the nested context for details.
* [Membership](Membership/context.md) – See the nested context for details.
* [Messages](Messages/context.md) – See the nested context for details.
* [Metrics](Metrics/context.md) – See the nested context for details.
* [Partition](Partition/context.md) – See the nested context for details.
* [Partition Activator](PartitionActivator/context.md) – See the nested context for details.
* [Properties](Properties/context.md) – See the nested context for details.
* [Pub Sub](PubSub/context.md) – See the nested context for details.
* [Seed](Seed/context.md) – See the nested context for details.
* [Single Node](SingleNode/context.md) – See the nested context for details.

