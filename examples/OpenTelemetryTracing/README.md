# OpenTelemetryTracing

Demonstrates Proto.Actor tracing with OpenTelemetry and a Jaeger exporter. The sample runs two actor systems communicating over gRPC Remote and shows trace context flowing from a local actor to a remote actor.

## Run

1. Start Jaeger:

   ```bash
   docker run --rm -p 16686:16686 -p 6831:6831/udp jaegertracing/all-in-one:1.39
   ```

2. Run the example:

   ```bash
   dotnet run --project examples/OpenTelemetryTracing
   ```

3. Open [http://localhost:16686](http://localhost:16686) and look for the `OpenTelemetryTracingSample` service to see spans for the root, local actor and remote actor belonging to the same trace.
