# Proto Open Telemetry Tests Context

## Overview
Tests for the OpenTelemetry instrumentation package.

_Parent context: [Tests](../context.md)_

## Key Files
* `ActivityFixture.cs` – C# source defining Activity Fixture behavior.
* `OpenTelemetryMetricsTests.cs` – C# source defining Open Telemetry Metrics Tests behavior.
* `OpenTelemetryTracingTests.cs` – C# source defining Open Telemetry Tracing Tests behavior.
* `Proto.OpenTelemetry.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `messages.proto` – Protocol Buffers schema.

## Primary Types and Contracts
* `Class ActivityFixture` defined in `ActivityFixture.cs`
* `Class OpenTelemetryMetricsTests` defined in `OpenTelemetryMetricsTests.cs`
* `Class EchoActor` defined in `OpenTelemetryMetricsTests.cs`
* `Class TestExporter` defined in `OpenTelemetryMetricsTests.cs`
* `Class OpenTelemetryTracingTests` defined in `OpenTelemetryTracingTests.cs`
* `Enum SendAs` defined in `OpenTelemetryTracingTests.cs`
* `Record TraceMe` defined in `OpenTelemetryTracingTests.cs`
* `Record TraceResponse` defined in `OpenTelemetryTracingTests.cs`
* `Class TraceTestActor` defined in `OpenTelemetryTracingTests.cs`

## Related Subcontexts
* No nested subcontexts.

