using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto;
using Xunit;

namespace Proto.Router.Tests;

public class BroadcastPoolRouterTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new RecordingActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastPoolRouter_RemovedRouteesDoNotReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewBroadcastPool(MyActorProps, 3);
        var router = system.Root.Spawn(props);

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        var routee1 = routees.Pids[0];
        var routee2 = routees.Pids[1];
        var routee3 = routees.Pids[2];

        system.Root.Send(router, "first");
        system.Root.Send(router, new RouterRemoveRoutee(routee1));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        await system.Root.RequestAsync<Touched>(routee1, new Touch(), _timeout);
        system.Root.Send(router, "second");

        Assert.Equal("first", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("second", await system.Root.RequestAsync<string>(routee2, "received?", _timeout));
        Assert.Equal("second", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    private class RecordingActor : IActor
    {
        private string? _received;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case "received?":
                    context.Respond(_received!);
                    break;
                case string s:
                    _received = s;
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
