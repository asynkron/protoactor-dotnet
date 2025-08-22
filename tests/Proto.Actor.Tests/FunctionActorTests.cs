using System.Threading.Tasks;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class FunctionActorTests
{
    [Fact]
    public async Task FunctionActorHandlesMessages()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, probePid) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is string)
            {
                ctx.Forward(probePid);
            }
            return Task.CompletedTask;
        });

        var pid = context.Spawn(props);
        context.Send(pid, "hello");
        await probe.ExpectNextUserMessageAsync<string>(msg => msg == "hello");
    }
}
