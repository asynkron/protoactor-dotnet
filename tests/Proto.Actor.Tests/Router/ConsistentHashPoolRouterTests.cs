using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto;
using Xunit;

namespace Proto.Router.Tests;

public class ConsistentHashPoolRouterTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new RespondWithSelfActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    private record Ping(string Id) : IHashable
    {
        public string HashBy() => Id;
    }

    [Fact]
    public async Task ConsistentHashPoolRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewConsistentHashPool(MyActorProps, 3);
        var router = system.Root.Spawn(props);

        var pid1 = await system.Root.RequestAsync<PID>(router, new Ping("a"), _timeout);
        var pid2 = await system.Root.RequestAsync<PID>(router, new Ping("b"), _timeout);
        var pid3 = await system.Root.RequestAsync<PID>(router, new Ping("a"), _timeout);

        Assert.Equal(pid1, pid3);
        Assert.NotEqual(pid1, pid2);
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
