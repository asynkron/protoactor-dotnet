<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-12-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->
| Status | History |
| :---   | :---    |
| ![Build status](https://ci.appveyor.com/api/projects/status/cmpnw19ur8j25xn4/branch/master?svg=true)|![Build history](https://buildstats.info/github/chart/asynkron/protoactor-dotnet)|


### [💬 Join our Slack channel](https://join.slack.com/t/asynkron/shared_invite/zt-ko824601-yGN1d3GHF9jzZX2VtONodQ)

# Proto.Actor

Ultra-fast, distributed, cross-platform actors.

## Bootcamp Training

[https://github.com/AsynkronIT/protoactor-bootcamp](https://github.com/AsynkronIT/protoactor-bootcamp)

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

## Sponsors
Our awesome sponsors:

<!-- sponsors --><a href="https://github.com/jhston02"><img src="https://github.com/jhston02.png" width="60px" alt="" /></a><a href="https://github.com/schafer14"><img src="https://github.com/schafer14.png" width="60px" alt="" /></a><a href="https://github.com/nbokovoy"><img src="https://github.com/nbokovoy.png" width="60px" alt="" /></a><!-- sponsors -->

## Contributors

<a href="https://github.com/AsynkronIT/protoactor-dotnet/graphs/contributors">
<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tr>
    <td align="center"><a href="http://asynkron.se"><img src="https://avatars.githubusercontent.com/u/647031?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Roger Johansson</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=rogeralsing" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/mhelleborg"><img src="https://avatars.githubusercontent.com/u/13994978?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Magne Helleborg</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=mhelleborg" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/cpx86"><img src="https://avatars.githubusercontent.com/u/209890?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Christian Palmstierna</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=cpx86" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/potterdai"><img src="https://avatars.githubusercontent.com/u/3758951?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Potter Dai</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=potterdai" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/tomliversidge"><img src="https://avatars.githubusercontent.com/u/1437372?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Tom Liversidge</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=tomliversidge" title="Code">💻</a></td>
    <td align="center"><a href="http://www.zimarev.com"><img src="https://avatars.githubusercontent.com/u/2821205?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Alexey Zimarev</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=alexeyzimarev" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/adamhathcock"><img src="https://avatars.githubusercontent.com/u/527620?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Adam Hathcock</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=adamhathcock" title="Code">💻</a></td>
  </tr>
  <tr>
    <td align="center"><a href="http://www.kompilera.se/"><img src="https://avatars.githubusercontent.com/u/5316125?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Daniel Söderberg</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=raskolnikoov" title="Code">💻</a></td>
    <td align="center"><a href="https://www.eventuallyconsultant.com"><img src="https://avatars.githubusercontent.com/u/2705498?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Jérôme Rouaix</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=jrouaix" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/Damian-P"><img src="https://avatars.githubusercontent.com/u/1333962?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Piechowicz Damian</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=Damian-P" title="Code">💻</a></td>
    <td align="center"><a href="https://github.com/alexpantyukhin"><img src="https://avatars.githubusercontent.com/u/6513121?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Alexander Pantyukhin</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=alexpantyukhin" title="Code">💻</a></td>
    <td align="center"><a href="http://www.lighthouselogic.com/"><img src="https://avatars.githubusercontent.com/u/1631623?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Sudsy</b></sub></a><br /><a href="https://github.com/asynkron/protoactor-dotnet/commits?author=sudsy" title="Code">💻</a></td>
  </tr>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
</a>

Made with [contributors-img](https://contributors-img.firebaseapp.com).
