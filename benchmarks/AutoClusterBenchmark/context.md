# Auto Cluster Benchmark Context

## Overview
Auto-clustering benchmark scenarios.

_Parent context: [Benchmarks](../context.md)_

## Key Files
* `AutoClusterBenchmark.csproj` – Project file configuring compilation targets and dependencies.
* `Configuration.cs` – C# source defining Configuration behavior.
* `Program.cs` – C# source defining Program behavior.
* `Runner.cs` – C# source defining Runner behavior.
* `WorkerActor.cs` – C# source defining Worker Actor behavior.
* `messages.proto` – Protocol Buffers schema.

## Primary Types and Contracts
* `Class Configuration` defined in `Configuration.cs`
* `Class Program` defined in `Program.cs`
* `Interface IRunMember` defined in `Runner.cs`
* `Class RunMemberInProcGraceful` defined in `Runner.cs`
* `Class WorkerActor` defined in `WorkerActor.cs`

## Related Subcontexts
* No nested subcontexts.

