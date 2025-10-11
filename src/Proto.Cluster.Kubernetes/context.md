# Proto Cluster Kubernetes Context

## Overview
Kubernetes-based cluster provider and tooling.

_Parent context: [Src](../context.md)_

## Key Files
* `KubernetesClusterMonitor.cs` – C# source defining Kubernetes Cluster Monitor behavior.
* `KubernetesExtensions.cs` – C# source defining Kubernetes Extensions behavior.
* `KubernetesHelper.cs` – C# source defining Kubernetes Helper behavior.
* `KubernetesProvider.cs` – C# source defining Kubernetes Provider behavior.
* `KubernetesProviderConfig.cs` – C# source defining Kubernetes Provider Config behavior.
* `Messages.cs` – C# source defining Messages behavior.
* `Proto.Cluster.Kubernetes.csproj` – Project file configuring compilation targets and dependencies.
* `ProtoLabels.cs` – C# source defining Proto Labels behavior.

## Primary Types and Contracts
* `Class KubernetesClusterMonitor` defined in `KubernetesClusterMonitor.cs`
* `Class KubernetesExtensions` defined in `KubernetesExtensions.cs`
* `Record MemberStatus` defined in `KubernetesExtensions.cs`
* `Class KubernetesHelper` defined in `KubernetesHelper.cs`
* `Class KubernetesProvider` defined in `KubernetesProvider.cs`
* `Record KubernetesProviderConfig` defined in `KubernetesProviderConfig.cs`
* `Class Messages` defined in `Messages.cs`
* `Class RegisterMember` defined in `Messages.cs`
* `Class DeregisterMember` defined in `Messages.cs`
* `Class StartWatchingCluster` defined in `Messages.cs`
* `Class ProtoLabels` defined in `ProtoLabels.cs`

## Related Subcontexts
* No nested subcontexts.

