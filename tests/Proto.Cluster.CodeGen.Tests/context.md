# Proto Cluster Code Gen Tests Context

## Overview
Tests covering the code generator and templates.

_Parent context: [Tests](../context.md)_

## Key Files
* `ExpectedOutput.cs` – C# source defining Expected Output behavior.
* `ExpectedOutputPackageless.cs` – C# source defining Expected Output Packageless behavior.
* `OutputFileNameTests.cs` – C# source defining Output File Name Tests behavior.
* `PathPolyfillTests.cs` – C# source defining Path Polyfill Tests behavior.
* `Proto.Cluster.CodeGen.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `ProtoGrainGenerationTests.cs` – C# source defining Proto Grain Generation Tests behavior.
* `bar.proto` – Protocol Buffers schema.
* `foo.proto` – Protocol Buffers schema.
* `foo_packageless.proto` – Protocol Buffers schema.
* `invalid.proto` – Protocol Buffers schema.
* `invalid2.proto` – Protocol Buffers schema.

## Primary Types and Contracts
* `Class GrainExtensions` defined in `ExpectedOutput.cs`
* `Class TestGrainBase` defined in `ExpectedOutput.cs`
* `Class TestGrainClient` defined in `ExpectedOutput.cs`
* `Class TestGrainActor` defined in `ExpectedOutput.cs`
* `Class GrainExtensions` defined in `ExpectedOutputPackageless.cs`
* `Class TestGrainBase` defined in `ExpectedOutputPackageless.cs`
* `Class TestGrainClient` defined in `ExpectedOutputPackageless.cs`
* `Class TestGrainActor` defined in `ExpectedOutputPackageless.cs`
* `Class OutputFileNameTests` defined in `OutputFileNameTests.cs`
* `Class PathPolyfillTests` defined in `PathPolyfillTests.cs`
* `Class ProtoGrainGenerationTests` defined in `ProtoGrainGenerationTests.cs`

## Related Subcontexts
* No nested subcontexts.

