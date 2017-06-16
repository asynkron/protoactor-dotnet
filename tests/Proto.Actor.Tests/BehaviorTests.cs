using System;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class BehaviorTests
    {
        [Fact]
        public async void can_change_states()
        {
            var testActorProps = Actor.FromProducer(() => new LightBulb());
            var actor = Actor.Spawn(testActorProps);
            
            var response = await actor.RequestAsync<string>(new PressSwitch());
            Assert.Equal("Turning on", response);
            response = await actor.RequestAsync<string>(new Touch());
            Assert.Equal("Hot!", response);
            response = await actor.RequestAsync<string>(new PressSwitch());
            Assert.Equal("Turning off", response);
            response = await actor.RequestAsync<string>(new Touch());
            Assert.Equal("Cold", response);
        }
        
        [Fact]
        public async void can_use_global_behaviour()
        {
            var testActorProps = Actor.FromProducer(() => new LightBulb());
            var actor = Actor.Spawn(testActorProps);
            var response = await actor.RequestAsync<string>(new PressSwitch());
            response = await actor.RequestAsync<string>(new HitWithHammer());
            Assert.Equal("Smashed!", response);
            response = await actor.RequestAsync<string>(new PressSwitch());
            Assert.Equal("Broken", response);
            response = await actor.RequestAsync<string>(new Touch());
            Assert.Equal("OW!", response);
        }
        
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));

        [Fact]
        public async Task pop_behavior_should_restore_pushed_behavior()
        {
            var behavior = new Behavior();
            behavior.Become(ctx =>
            {
                if (ctx.Message is string)
                {
                    behavior.BecomeStacked(async ctx2 =>
                    {
                        await ctx2.RespondAsync(42);
                        behavior.UnbecomeStacked();
                    });
                    return ctx.RespondAsync(ctx.Message);
                }
                return Actor.Done;
            });
            PID pid = SpawnActorFromFunc(behavior.ReceiveAsync);

            var reply = await pid.RequestAsync<string>("number");
            var replyAfterPush = await pid.RequestAsync<int>(null);
            var replyAfterPop = await pid.RequestAsync<string>("answertolifetheuniverseandeverything");

            Assert.Equal("number42answertolifetheuniverseandeverything", $"{reply}{replyAfterPush}{replyAfterPop}");
        }
    }
    
    public class LightBulb : IActor{
        private readonly Behavior _behavior;
        private bool _smashed;

        public LightBulb()
        {
            _behavior = new Behavior();
            _behavior.Become(Off);
        }

        private async Task Off(IContext context)
        {
            switch (context.Message)
            {
                case PressSwitch _:
                    await context.RespondAsync("Turning on");
                    _behavior.Become(On);
                    break;
                case Touch _:
                    await context.RespondAsync("Cold");
                    break;
            }
        }
        
        private async Task On(IContext context)
        {
            switch (context.Message)
            {
                case PressSwitch _:
                    await context.RespondAsync("Turning off");
                    _behavior.Become(Off);
                    break;
                case Touch _:
                    await context.RespondAsync("Hot!");
                    break;
            }
        }

        public async Task ReceiveAsync(IContext context)
        {
            // any "global" message handling here
            switch (context.Message)
            {
                case HitWithHammer _:
                    await context.RespondAsync("Smashed!");
                    _smashed = true;
                    return;
                case PressSwitch _ when _smashed:
                    await context.RespondAsync("Broken");
                    return;
                case Touch _ when _smashed:
                    await context.RespondAsync("OW!");
                    return;
            }
            // if not handled, use behavior specific
            await _behavior.ReceiveAsync(context);
        }
    }

    public class PressSwitch {}
    public class Touch {}
    public class HitWithHammer {}
}