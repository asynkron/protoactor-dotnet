using System;
using System.Threading.Tasks;
using Proto;
using Messages;
using Microsoft.Extensions.Logging;

namespace Worker
{
    public class HelloActor : IActor
    {
        static Random rnd = new Random();
        private readonly ILogger<HelloActor> logger;

        public HelloActor(ILogger<HelloActor> logger)
        {
            this.logger = logger;
        }

        public Task ReceiveAsync(IContext ctx)
        {
            switch (ctx.Message)
            {
                case Started _:
                    logger.LogDebug($"Started {ctx.Self}");
                    ctx.SetReceiveTimeout(TimeSpan.FromSeconds(rnd.Next(10, 1000)));
                    break;
                case HelloRequest _:
                    ctx.Respond(new HelloResponse());
                    break;
                case ReceiveTimeout _:
                    ctx.Stop(ctx.Self!);
                    break;
                case Stopped _:
                    logger.LogDebug($"Stopped {ctx.Self}");
                    break;
                default:
                    break;
            }
            return Actor.Done;
        }
    }
}