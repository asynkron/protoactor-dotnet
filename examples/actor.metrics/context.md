# Actor Metrics Context

## Overview
Actor metrics integration with Prometheus/Grafana.

_Parent context: [Examples](../context.md)_

## Key Files
* `ActorMetrics.csproj` – Project file configuring compilation targets and dependencies.
* `Messages.proto` – Protocol Buffers schema.
* `Program.cs` – C# source defining Program behavior.
* `README.md` – Markdown documentation.
* `RunDummyCluster.cs` – C# source defining Run Dummy Cluster behavior.
* `Startup.cs` – C# source defining Startup behavior.
* `WeatherForecast.cs` – C# source defining Weather Forecast behavior.
* `appsettings.Development.json` – Configuration or metadata in JSON format.
* `appsettings.json` – Configuration or metadata in JSON format.
* `docker-compose.yaml` – YAML configuration.
* `prometheus.yml` – YAML configuration.

## Primary Types and Contracts
* `Class Program` defined in `Program.cs`
* `Class RunDummyCluster` defined in `RunDummyCluster.cs`
* `Record MyMessage` defined in `RunDummyCluster.cs`
* `Class MyActor` defined in `RunDummyCluster.cs`
* `Class Startup` defined in `Startup.cs`
* `Class WeatherForecast` defined in `WeatherForecast.cs`

## Related Subcontexts
* [Controllers](Controllers/context.md) – See the nested context for details.
* [Properties](Properties/context.md) – See the nested context for details.
* [Grafana](grafana/context.md) – See the nested context for details.

