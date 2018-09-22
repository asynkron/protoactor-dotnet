using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class BehaviorTests
    {
        private static readonly RootContext Context = new RootContext();
        
        [Fact]
        public async void can_change_states()
        {
            var testActorProps = Props.FromProducer(() => new LightBulb());
            var actor = Context.Spawn(testActorProps);
            
            var response = await Context.RequestAsync<string>(actor, new PressSwitch());
            Assert.Equal("Turning on", response);
            response = await Context.RequestAsync<string>(actor, new Touch());
            Assert.Equal("Hot!", response);
            response = await Context.RequestAsync<string>(actor, new PressSwitch());
            Assert.Equal("Turning off", response);
            response = await Context.RequestAsync<string>(actor, new Touch());
            Assert.Equal("Cold", response);
        }
        
        [Fact]
        public async void can_use_global_behaviour()
        {
            var testActorProps = Props.FromProducer(() => new LightBulb());
            var actor = Context.Spawn(testActorProps);
            var _ = await Context.RequestAsync<string>(actor, new PressSwitch());
            var response = await Context.RequestAsync<string>(actor, new HitWithHammer());
            Assert.Equal("Smashed!", response);
            response = await Context.RequestAsync<string>(actor, new PressSwitch());
            Assert.Equal("Broken", response);
            response = await Context.RequestAsync<string>(actor, new Touch());
            Assert.Equal("OW!", response);
        }
        
        public static PID SpawnActorFromFunc(Receive receive) => Context.Spawn(Props.FromFunc(receive));

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

            var reply = await Context.RequestAsync<string>(pid, "number");
            var replyAfterPush = await Context.RequestAsync<int>(pid, null);
            var replyAfterPop = await Context.RequestAsync<string>(pid, "answertolifetheuniverseandeverything");

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