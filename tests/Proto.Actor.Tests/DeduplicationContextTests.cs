using System;
using System.Threading.Tasks;
using Proto.Deduplication;
using Proto;
using Xunit;

namespace Proto.Tests;

public class DeduplicationContextTests
{
    [Fact]
    public async Task DuplicatesAreIgnored()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var count = 0;

        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is string)
            {
                count++;
            }
            return Task.CompletedTask;
        }).WithContextDecorator(c => new DeduplicationContext<string>(
            c,
            TimeSpan.FromSeconds(5),
            (MessageEnvelope env, out string? key) =>
            {
                key = env.Message as string;
                return key != null;
            }
        ));

        var pid = context.Spawn(props);
        context.Send(pid, "one");
        context.Send(pid, "one");
        context.Send(pid, "two");
        await Task.Delay(100);

        Assert.Equal(2, count);
    }
}
