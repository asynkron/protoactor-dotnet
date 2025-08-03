using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests;

public class FunctionActorTests
{
    [Fact]
    public async Task FunctionActorHandlesMessages()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var received = false;

        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is string)
            {
                received = true;
            }
            return Task.CompletedTask;
        });

        var pid = context.Spawn(props);
        context.Send(pid, "hello");
        await Task.Delay(50);

        Assert.True(received);
    }
}
