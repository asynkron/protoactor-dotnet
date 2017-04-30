using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class PopPushBehaviorTests
    {
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));

        [Fact]
        public async Task PopBehaviorShouldRestorePushedBehavior()
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
}
