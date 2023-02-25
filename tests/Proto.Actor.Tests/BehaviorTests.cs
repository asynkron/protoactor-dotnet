using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests;

public class BehaviorTests
{
    [Fact]
    public async Task can_change_states()
    {
        var system = new ActorSystem();
        await using var _ = system.ConfigureAwait(false);
        var context = system.Root;

        var testActorProps = Props.FromProducer(() => new LightBulb());
        var actor = context.Spawn(testActorProps);

        var response = await context.RequestAsync<string>(actor, new PressSwitch()).ConfigureAwait(false);
        Assert.Equal("Turning on", response);
        response = await context.RequestAsync<string>(actor, new Touch()).ConfigureAwait(false);
        Assert.Equal("Hot!", response);
        response = await context.RequestAsync<string>(actor, new PressSwitch()).ConfigureAwait(false);
        Assert.Equal("Turning off", response);
        response = await context.RequestAsync<string>(actor, new Touch()).ConfigureAwait(false);
        Assert.Equal("Cold", response);
    }

    [Fact]
    public async Task can_use_global_behaviour()
    {
        var system = new ActorSystem();
        await using var __ = system.ConfigureAwait(false);
        var context = system.Root;

        var testActorProps = Props.FromProducer(() => new LightBulb());
        var actor = context.Spawn(testActorProps);
        var _ = await context.RequestAsync<string>(actor, new PressSwitch()).ConfigureAwait(false);
        var response = await context.RequestAsync<string>(actor, new HitWithHammer()).ConfigureAwait(false);
        Assert.Equal("Smashed!", response);
        response = await context.RequestAsync<string>(actor, new PressSwitch()).ConfigureAwait(false);
        Assert.Equal("Broken", response);
        response = await context.RequestAsync<string>(actor, new Touch()).ConfigureAwait(false);
        Assert.Equal("OW!", response);
    }

    [Fact]
    public async Task pop_behavior_should_restore_pushed_behavior()
    {
        var system = new ActorSystem();
        await using var _ = system.ConfigureAwait(false);
        var context = system.Root;

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        var behavior = new Behavior();

        behavior.Become(ctx =>
            {
                if (ctx.Message is string)
                {
                    behavior.BecomeStacked(ctx2 =>
                        {
                            ctx2.Respond(42);
                            behavior.UnbecomeStacked();

                            return Task.CompletedTask;
                        }
                    );

                    ctx.Respond(ctx.Message);
                }

                return Task.CompletedTask;
            }
        );

        var pid = SpawnActorFromFunc(behavior.ReceiveAsync);

        var reply = await context.RequestAsync<string>(pid, "number").ConfigureAwait(false);
        var replyAfterPush = await context.RequestAsync<int>(pid, null!).ConfigureAwait(false);
        var replyAfterPop = await context.RequestAsync<string>(pid, "answertolifetheuniverseandeverything").ConfigureAwait(false);

        Assert.Equal("number42answertolifetheuniverseandeverything", $"{reply}{replyAfterPush}{replyAfterPop}");
    }
}

public class LightBulb : IActor
{
    private readonly Behavior _behavior;
    private bool _smashed;

    public LightBulb()
    {
        _behavior = new Behavior();
        _behavior.Become(Off);
    }

    public Task ReceiveAsync(IContext context)
    {
        // any "global" message handling here
        switch (context.Message)
        {
            case HitWithHammer _:
                context.Respond("Smashed!");
                _smashed = true;

                return Task.CompletedTask;
            case PressSwitch _ when _smashed:
                context.Respond("Broken");

                return Task.CompletedTask;
            case Touch _ when _smashed:
                context.Respond("OW!");

                return Task.CompletedTask;
        }

        // if not handled, use behavior specific
        return _behavior.ReceiveAsync(context);
    }

    private Task Off(IContext context)
    {
        switch (context.Message)
        {
            case PressSwitch _:
                context.Respond("Turning on");
                _behavior.Become(On);

                break;
            case Touch _:
                context.Respond("Cold");

                break;
        }

        return Task.CompletedTask;
    }

    private Task On(IContext context)
    {
        switch (context.Message)
        {
            case PressSwitch _:
                context.Respond("Turning off");
                _behavior.Become(Off);

                break;
            case Touch _:
                context.Respond("Hot!");

                break;
        }

        return Task.CompletedTask;
    }
}

public class PressSwitch
{
}

public class Touch
{
}

public class HitWithHammer
{
}