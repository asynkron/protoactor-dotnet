[![Build status](https://ci.appveyor.com/api/projects/status/22q8a3e8ovejl5qf/branch/master?svg=true)](https://ci.appveyor.com/project/cpx/protoactor-dotnet-pr5pp/branch/master)

[![Join the chat at https://gitter.im/AsynkronIT/protoactor](https://badges.gitter.im/AsynkronIT/protoactor.svg)](https://gitter.im/AsynkronIT/protoactor?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

# Proto.Actor
Ultra fast distributed actors for .NET and Go

## Sourcecode - C#
This is the .NET repository for Proto Actor.

The Go implementation can be found here [https://github.com/AsynkronIT/protoactor-go](https://github.com/AsynkronIT/protoactor-go)

## Design principles:

**Minimalistic API** -
The API should be small and easy to use.
Avoid enterprisey JVM like containers and configurations.

**Build on existing technologies** - There are already a lot of great tech for e.g. networking and clustering, build on those.
e.g. gRPC streams for networking, Consul.IO for clustering.

**Pass data, not objects** - Serialization is an explicit concern, don't try to hide it.
Protobuf all the way.

**Be fast** - Do not trade performance for magic API trickery.

## Hello world

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

For more examples, see the examples folder in this repository.

## NuGet

`PM> Install-Package Proto.Actor`

