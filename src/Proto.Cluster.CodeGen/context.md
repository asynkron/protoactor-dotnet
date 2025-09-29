# Proto Cluster Code Gen Context

## Overview
Source generator and command-line tooling for Proto.Cluster grains.

_Parent context: [Src](../context.md)_

## Key Files
* `CodeGenerator.cs` – C# source defining Code Generator behavior.
* `Generator.cs` – C# source defining Generator behavior.
* `OutputFileName.cs` – C# source defining Output File Name behavior.
* `PathPolyfill.cs` – C# source defining Path Polyfill behavior.
* `Proto.Cluster.CodeGen.csproj` – Project file configuring compilation targets and dependencies.
* `ProtoGenTask.cs` – C# source defining Proto Gen Task behavior.
* `README.md` – Markdown documentation.
* `Template.cs` – C# source defining Template behavior.
* `build.sh` – Script file.
* `logo.png` – Image asset.

## Primary Types and Contracts
* `Class CodeGenerator` defined in `CodeGenerator.cs`
* `Class Generator` defined in `Generator.cs`
* `Class OutputFileName` defined in `OutputFileName.cs`
* `Class PathPolyfill` defined in `PathPolyfill.cs`
* `Class ProtoGenTask` defined in `ProtoGenTask.cs`
* `Class Template` defined in `Template.cs`
* `Class GrainExtensions` defined in `Template.cs`

## Related Subcontexts
* [Properties](Properties/context.md) – See the nested context for details.
* [Build](build/context.md) – See the nested context for details.
* [Build Multi Targeting](buildMultiTargeting/context.md) – See the nested context for details.
* [Deps](deps/context.md) – See the nested context for details.
* [Model](model/context.md) – See the nested context for details.

