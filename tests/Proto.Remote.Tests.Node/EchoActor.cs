using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests.Node
{
    public class EchoActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<EchoActor>();

        private readonly string _host;
        private readonly int _port;

        public EchoActor(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Logger.LogDebug($"{context.Self}");
                    break;
                case Ping ping:
                    Logger.LogDebug("Received Ping, replying Pong");
                    context.Respond(new Pong { Message = $"{_host}:{_port} {ping.Message}" });
                    break;
                case Die _:
                    Logger.LogDebug("Received termination request, stopping");
                    context.Stop(context.Self);
                    break;
                default:
                    Logger.LogDebug(context.Message.GetType().Name);
                    break;
            }

            return Actor.Done;
        }
    }
}