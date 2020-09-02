using System;
using System.Threading.Tasks;
using Proto;
using Messages;
using Microsoft.Extensions.Logging;

namespace Worker
{
    public class HelloActor : IActor
    {
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
                    logger.LogInformation($"Started {ctx.Self}");
                    ctx.SetReceiveTimeout(TimeSpan.FromSeconds(5));
                    break;
                case HelloRequest _:
                    ctx.Respond(new HelloResponse());
                    break;
                case ReceiveTimeout _:
                    ctx.Stop(ctx.Self!);
                    break;
                case Stopped _:
                    logger.LogInformation($"Stopped {ctx.Self}");
                    break;
                default:
                    break;
            }
            return Actor.Done;
        }
    }
}