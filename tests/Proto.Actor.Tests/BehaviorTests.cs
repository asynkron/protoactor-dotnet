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
                    behavior.BecomeStacked(ctx2 =>
                    {
                        ctx2.Respond(42);
                        behavior.UnbecomeStacked();
                        return Actor.Done;
                    });
                    ctx.Respond(ctx.Message);
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
            
            return Actor.Done;
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
            
            return Actor.Done;
        }

        public Task ReceiveAsync(IContext context)
        {
            // any "global" message handling here
            switch (context.Message)
            {
                case HitWithHammer _:
                    context.Respond("Smashed!");
                    _smashed = true;
                    return Actor.Done;
                case PressSwitch _ when _smashed:
                    context.Respond("Broken");
                    return Actor.Done;
                case Touch _ when _smashed:
                    context.Respond("OW!");
                    return Actor.Done;
            }
            // if not handled, use behavior specific
            return _behavior.ReceiveAsync(context);
        }
    }

    public class PressSwitch {}
    public class Touch {}
    public class HitWithHammer {}
}