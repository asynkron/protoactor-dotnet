using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote.Tests.Messages;

namespace Proto.Cluster.Tests
{
    using ClusterTest.Messages;

    public class EchoActor : IActor
    {
        public const string Kind = "echo";

        public static readonly Props Props = Props.FromProducer(() => new EchoActor());
        private static readonly ILogger Logger = Log.CreateLogger<EchoActor>();

        private string _identity;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Logger.LogDebug($"{context.Self}");
                    break;
                case ClusterInit init:
                    _identity = init.Identity;
                    break;
                case Ping ping:
                    Logger.LogDebug("Received Ping, replying Pong");
                    context.Respond(new Pong {Message = $"{_identity}:{ping.Message}"});
                    break;
                case WhereAreYou _:
                    Logger.LogDebug("Responding to location request");
                    context.Respond(new HereIAm {Address = context.Self!.Address});
                    break;
                case Die _:
                    Logger.LogDebug("Received termination request, stopping");
                    context.Respond(new Ack());
                    context.Stop(context.Self!);
                    break;
                default:
                    Logger.LogDebug(context.Message?.GetType().Name);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}