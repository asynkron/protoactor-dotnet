using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests;

public class SharedFutureBugTests
{
    [Fact]
    public async Task Should_get_unique_future()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var count = 100_000;
        var hashSet = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var f = context.GetFuture();
            var s = f.Pid.ToDiagnosticString();
            hashSet.Add(s);
        }

        hashSet.Count.Should().Be(count);
    }
}