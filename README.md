[![Build status](https://ci.appveyor.com/api/projects/status/cmpnw19ur8j25xn4/branch/master?svg=true)](https://ci.appveyor.com/project/protoactor-ci/protoactor-dotnet/branch/master)

[![Join the chat at https://gitter.im/AsynkronIT/protoactor](https://badges.gitter.im/AsynkronIT/protoactor.svg)](https://gitter.im/AsynkronIT/protoactor?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

# Proto.Actor
Ultra-fast, distributed, cross-platform actors.

## Installing

Using NuGet Package Manager Console:
`PM> Install-Package Proto.Actor`

## Source code
This is the .NET repository for Proto Actor.

Other implementations:
* Go: [https://github.com/AsynkronIT/protoactor-go](https://github.com/AsynkronIT/protoactor-go)
* Python (unstable/WIP): [https://github.com/AsynkronIT/protoactor-python](https://github.com/AsynkronIT/protoactor-python)
* JavaScript (unstable/WIP): [https://github.com/AsynkronIT/protoactor-js](https://github.com/AsynkronIT/protoactor-js)

## How to build

Proto.Actor uses and requires the VS2017 build system in order to build. You can either use the `dotnet` CLI commands, or use Visual Studio 2017.

We also use [Cake](http://cakebuild.net/) for orchestrating the CI builds. The CI build basically runs `dotnet restore`, `dotnet build`, `dotnet test` and `dotnet pack`. To run a full CI build execute either `.\build.ps1` or `./build.sh`, depending on your environment.

## Design principles

**Minimalistic API** - The API should be small and easy to use. Avoid enterprisey containers and configurations.

**Build on existing technologies** - There are already a lot of great technologies for e.g. networking and clustering. Build on those instead of reinventing them. E.g. gRPC streams for networking, Consul for clustering.

**Pass data, not objects** - Serialization is an explicit concern - don't try to hide it. Protobuf all the way.

**Be fast** - Do not trade performance for magic API trickery.

## Getting started

The best place currently for learning how to use Proto.Actor is the [examples](https://github.com/AsynkronIT/protoactor-dotnet/tree/dev/examples). Documentation and guidance is under way, but not yet complete, and can be found on the [website](http://proto.actor/docs/dotnet/).

### Hello world

Define a message type:

```csharp
internal class Hello
{
    public string Who;
}
```

Define an actor:

```csharp
internal class HelloActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        var msg = context.Message;
        if (msg is Hello r)
        {
            Console.WriteLine($"Hello {r.Who}");
        }
        return Actor.Done;
    }
}
```

Spawn it and send a message to it:

```csharp
var props = Actor.FromProducer(() => new HelloActor());
var pid = Actor.Spawn(props);
pid.Tell(new Hello
{
    Who = "Alex"
});
```

You should see the output `Hello Alex`.