using System;
using System.Threading.Tasks;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class SupervisionTestsExponentialBackoff
{
    [Fact]
    public void FailureOutsideWindow_ResetsFailureCount()
    {
        var rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(11)));
        var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        strategy.HandleFailure(null!, null!, rs, null!, null);
        Assert.Equal(1, rs.FailureCount);
    }

    [Fact]
    public void FailureInsideWindow_IncrementsFailureCount()
    {
        var rs = new RestartStatistics(0, DateTime.Now);
        var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        strategy.HandleFailure(null!, null!, rs, null!, null);
        strategy.HandleFailure(null!, null!, rs, null!, null);
        strategy.HandleFailure(null!, null!, rs, null!, null);
        Assert.Equal(3, rs.FailureCount);
    }

    [Fact]
    public async Task ExponentialBackoffStrategy_Should_RestartChild()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var childMailboxStats = new TestMailboxStats(msg => msg is Stopped);
        var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50));

        var childProps = Props.FromProducer(() => new BackoffChild())
            .WithTestMailboxStats(childMailboxStats);

        var parentProps = Props.FromProducer(() => new ParentActor(childProps))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(parentProps);

        context.Send(parent, "fail");
        childMailboxStats.Reset.Wait(5000);

        Assert.Contains(childMailboxStats.Posted, m => m is Restart);
        Assert.Contains(childMailboxStats.Received, m => m is Restart);
    }

    private class ParentActor : IActor
    {
        private readonly Props _childProps;

        public ParentActor(Props childProps) => _childProps = childProps;

        private PID? Child { get; set; }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                Child = context.Spawn(_childProps);
            }

            if (context.Message is string)
            {
                context.Forward(Child!);
            }

            return Task.CompletedTask;
        }
    }

    private class BackoffChild : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string)
            {
                throw new Exception("boom");
            }

            return Task.CompletedTask;
        }
    }
}