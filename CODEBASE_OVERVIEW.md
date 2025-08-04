# Proto.Actor Codebase Overview

`protoactor-dotnet` is a fully fledged actor framework for .NET with a focus on high performance, simple APIs, and distributed scenarios. The repository is organized into modular packages that can be used independently or together.

## Repository Structure

- `src/` – library projects
  - `Proto.Actor` – core actor system, props, mailboxes, event stream, supervision, etc.
  - `Proto.Remote` – gRPC-based remoting for actors across nodes.
  - `Proto.Cluster` – clustering support with membership, identity lookup, partitioning, pub/sub, etc.
  - `Proto.Persistence` – persistence abstractions and providers (Couchbase, DynamoDB, MongoDB, SQLServer, etc.).
  - Additional modules such as `Proto.OpenTelemetry`, `Proto.TestKit`, `Proto.Analyzers`, code generators.
- `tests/` – unit and integration test projects for core and extensions.
- `examples/` – self-contained examples like HelloWorld, supervision patterns, and cluster scenarios.
- Other files include solution files (`*.sln`), build configuration, Docker files and documentation.

## Key Concepts

1. **Actor Model**
   - Implement `IActor` and `ReceiveAsync` to handle messages.
   - Actors are started through `ActorSystem` and `Props`.
   - Communication is performed via `PID` and messages.
2. **Serialization and Contracts**
   - Protobuf is the preferred format; define message types in `.proto` files.
3. **Distributed Features**
   - `Proto.Remote` uses gRPC to enable communication across nodes.
   - `Proto.Cluster` provides membership, automatic `PID` discovery, pub/sub and partitioning.
   - `Proto.Persistence` offers pluggable storage providers for stateful actors.
4. **Extensibility**
   - Extension points (`ActorSystemExtensions`, etc.) allow custom components.
   - Roslyn analyzers and code generators help with strongly typed grains and error detection.
5. **System Messages and Supervision**
   - Unrecognized system messages cause the actor to escalate a failure to its supervisor.

## Next Steps

1. Run through the examples in `examples/HelloWorld`, followed by advanced scenarios such as `ClusterGrainHelloWorld` or `Persistence`.
2. Read test projects under `tests/` to see usage patterns and edge cases.
3. Explore the [Proto Actor Bootcamp](https://proto.actor/docs/bootcamp/).
4. Dive into modules like `Proto.Remote`, `Proto.Cluster`, or `Proto.Persistence` once the basics are clear.
5. Browse the solution (`ProtoActor.sln`), run local tests, and check issues/PRs for current development.

## Using Docker Compose for Tests and Examples

Several test and example projects include a `docker-compose.yml` to spin up required services (databases, brokers, etc.).

- For tests: navigate to the test project directory and run  
  ```bash
  docker compose up -d
  dotnet test
  ```
- For examples: navigate to the example directory and run  
  ```bash
  docker compose up -d
  dotnet run
  ```

This ensures that dependencies like Redis, MongoDB, or Kafka are available before running the code.

