using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests
{
    public class EchoActor : IActor
    {
        private readonly ILogger _logger;

        public EchoActor(ActorSystem system)
        {
            _logger = system.LoggerFactory().CreateLogger<EchoActor>();
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug($"{context.Self}");
                    break;
                case Ping ping:
                    _logger.LogDebug("Received Ping, replying Pong");
                    context.Respond(new Pong {Message = $"{context.System.Address} {ping.Message}"});
                    break;
                case Die _:
                    _logger.LogDebug("Received termination request, stopping");
                    context.Stop(context.Self);
                    break;
                default:
                    _logger.LogDebug(context.Message.GetType().Name);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}