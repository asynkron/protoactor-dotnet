using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.TestKit;
using Proto;
using Xunit;

namespace Proto.Router.Tests;

public class BroadcastPoolRouterTests
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastPoolRouter_RemovedRouteesDoNotReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var probes = new List<TestProbe>();
        var routeeProps = Props.FromProducer(() =>
        {
            var probe = new TestProbe();
            probes.Add(probe);
            return probe;
        });

        var routerProps = system.Root.NewBroadcastPool(routeeProps, 3);
        var router = system.Root.Spawn(routerProps);

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        var routee1 = routees.Pids[0];
        var routee2 = routees.Pids[1];
        var routee3 = routees.Pids[2];

        var probe1 = probes.Find(p => (PID)p == routee1)!;
        var probe2 = probes.Find(p => (PID)p == routee2)!;
        var probe3 = probes.Find(p => (PID)p == routee3)!;

        system.Root.Send(router, "first");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "first");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "first");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "first");

        system.Root.Send(router, new RouterRemoveRoutee(routee1));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        await system.Root.RequestAsync<Touched>(routee1, new Touch(), _timeout);
        await probe1.ExpectNextUserMessageAsync<Touch>();

        system.Root.Send(router, "second");

        await probe2.ExpectNextUserMessageAsync<string>(x => x == "second");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "second");
        await probe1.ExpectNoMessageAsync();
    }
}
