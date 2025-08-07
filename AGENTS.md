# Agent Instructions

- Ensure we have dotnet 8 already installet. check dotnet --version
- Ensure we have Redis running. 
- Ensure we have Mongodb running.
- Ensure we have Consul running.
- Compile the full project before completing any task: `dotnet build ProtoActor.sln`.
- if some dependency; redis, mongodb, consul is missing, ignore associated tests
- Run the full suite of .NET tests for the solution: `dotnet test ProtoActor.sln`.

