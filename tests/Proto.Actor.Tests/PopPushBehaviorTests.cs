using System;
using System.Collections.Generic;
using System.Text;
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
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.PushBehavior(ctx2 =>
                    {                        
                        ctx2.Respond(42);
                        ctx2.PopBehavior();
                        return Actor.Done;
                    });
                    ctx.Respond(ctx.Message);
                }                
                return Actor.Done;
            });
            
            var reply = await pid.RequestAsync<string>("number");
            var replyAfterPush = await pid.RequestAsync<int>(null);
            var replyAfterPop = await pid.RequestAsync<string>("answertolifetheuniverseandeverything");

            Assert.Equal("number42answertolifetheuniverseandeverything", $"{reply}{replyAfterPush}{replyAfterPop}");
        }
    }
}
