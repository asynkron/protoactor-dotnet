### [💬 Join our Slack channel](https://join.slack.com/t/asynkron/shared_invite/zt-ko824601-yGN1d3GHF9jzZX2VtONodQ)

# Proto.Actor

Ultra-fast, distributed, cross-platform actors.

## Bootcamp Training

[https://github.com/AsynkronIT/protoactor-bootcamp](https://github.com/AsynkronIT/protoactor-bootcamp)

## Stats

![Alt](https://repobeats.axiom.co/api/embed/c9c21a6a706eda331cc8a38e4f03a7a844ed95f3.svg "Repobeats analytics image")

## Installing

Using NuGet Package Manager Console:

`PM> Install-Package Proto.Actor`

## Source code

This is the .NET repository for Proto Actor.

Other implementations:

- Go: [https://github.com/AsynkronIT/protoactor-go](https://github.com/AsynkronIT/protoactor-go)

## Design principles

**Minimalistic API** - The API should be small and easy to use. Avoid enterprisey containers and configurations.

**Build on existing technologies** - There are already a lot of great technologies for e.g. networking and clustering. Build on those instead of reinventing them. E.g. gRPC streams for networking, Consul for clustering.

**Pass data, not objects** - Serialization is an explicit concern - don't try to hide it. Protobuf all the way.

**Be fast** - Do not trade performance for magic API trickery.

## Getting started

The best place currently for learning how to use Proto.Actor is the [examples](https://github.com/AsynkronIT/protoactor-dotnet/tree/dev/examples). Documentation and guidance is under way, but not yet complete, and can be found on the [website](https://proto.actor/docs/).

### Hello world

Define a message type:

```csharp
internal record Hello(string Who);
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
        return Task.CompletedTask;
    }
}
```

Spawn it and send a message to it:

```csharp
var system = new ActorSystem();
var context = system.Root;
var props = Props.FromProducer(() => new HelloActor());
var pid = context.Spawn(props);

context.Send(pid, new Hello("Alex"));
```

You should see the output `Hello Alex`.

## Sample application

[https://github.com/asynkron/realtimemap-dotnet](https://github.com/asynkron/realtimemap-dotnet)

## Contributors

<a href="https://github.com/asynkron/protoactor-dotnet/graphs/contributors">
  <img src="https://contributors-img.web.app/image?repo=asynkron/protoactor-dotnet" />
</a>

Made with [contributors-img](https://contributors-img.web.app).

## Partners, Sponsors, and Contributor Companies

<!-- make pretty with logos etc -->

| Name                                     | Role                                  |
| ---------------------------------------- | ------------------------------------- |
| [Asynkron AB](https://asynkron.se)       | Founder and owner of Proto.Actor      |
| Helleborg AS                             | Core contributor team                 |
| [Ubiquitous AS](https://ubiquitous.no/)  | Core contributor team                 |
| [Ahoy Games](https://www.ahoygames.com/) | Core contributor team                 |
| [Etteplan](https://www.etteplan.com/)    | Contributing tutorials, documentation |
