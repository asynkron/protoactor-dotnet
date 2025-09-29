# Cluster Benchmark Context

## Overview
Cluster behavior benchmark harness.

_Parent context: [Benchmarks](../context.md)_

## Key Files
* `ClrMessages.cs` – C# source defining Clr Messages behavior.
* `ClusterBenchmark.csproj` – Project file configuring compilation targets and dependencies.
* `Configuration.cs` – C# source defining Configuration behavior.
* `DockerSupport.cs` – C# source defining Docker Support behavior.
* `Program.cs` – C# source defining Program behavior.
* `Runner.cs` – C# source defining Runner behavior.
* `WorkerActor.cs` – C# source defining Worker Actor behavior.
* `docker-compose.yml` – YAML configuration.
* `messages.proto` – Protocol Buffers schema.

## Primary Types and Contracts
* `Record HelloRequestPoco` defined in `ClrMessages.cs`
* `Record HelloResponsePoco` defined in `ClrMessages.cs`
* `Class Configuration` defined in `Configuration.cs`
* `Class DockerSupport` defined in `DockerSupport.cs`
* `Class Program` defined in `Program.cs`
* `Interface IRunMember` defined in `Runner.cs`
* `Class RunMemberInProcGraceful` defined in `Runner.cs`
* `Class RunMemberInProc` defined in `Runner.cs`
* `Class RunMemberExternalProcGraceful` defined in `Runner.cs`
* `Class RunMemberExternalProc` defined in `Runner.cs`
* `Class WorkerActor` defined in `WorkerActor.cs`

## Related Subcontexts
* [Logs](logs/context.md) – See the nested context for details.

