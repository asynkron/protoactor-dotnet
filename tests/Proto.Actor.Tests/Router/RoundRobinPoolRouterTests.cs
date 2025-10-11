using System;
using System.Linq;
using System.Threading.Tasks;
using Proto;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests;

public class RoundRobinPoolRouterTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new RespondWithSelfActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task RoundRobinPoolRouter_RoutesMessagesInRoundRobinOrder()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRoundRobinPool(MyActorProps, 3);
        var router = system.Root.Spawn(props);

        var first = await system.Root.RequestAsync<PID>(router, "1", _timeout);
        var second = await system.Root.RequestAsync<PID>(router, "2", _timeout);
        var third = await system.Root.RequestAsync<PID>(router, "3", _timeout);
        var fourth = await system.Root.RequestAsync<PID>(router, "4", _timeout);

        var firstThree = new[] { first, second, third };
        Assert.Equal(3, firstThree.Distinct().Count());
        Assert.Equal(first, fourth);
    }

    private class RespondWithSelfActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            context.Respond(context.Self);
            return Task.CompletedTask;
        }
    }
}
