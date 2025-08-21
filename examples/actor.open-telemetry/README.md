# Actor OpenTelemetry

Demonstrates Proto.Actor tracing and metrics using OpenTelemetry with console exporters. The sample runs a single actor system and prints spans and counters directly to the console, so no external collector is required. It also sends telemetry to an OTLP collector on `localhost:4317`, which can be visualized with [TraceLens](https://tracelens.io).

## Run

```bash
DOTNET_ReadyToRun=0 dotnet run --project examples/actor.open-telemetry
```

The program sends a message to an instrumented actor. The console output shows the trace identifier for the message and periodic dumps of Proto.Actor metrics.

## Visualize with TraceLens

Start TraceLens via Docker Compose to explore the emitted spans and counters:

```bash
curl -L https://raw.githubusercontent.com/asynkron/TraceLens/main/docker-compose.yml -o docker-compose.yml
docker compose up
```

With TraceLens running, execute the example again. Open http://localhost:5001 to inspect traces and metrics.
