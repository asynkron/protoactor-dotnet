using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            var context = new RootContext();
            var app = new CommandLineApplication();
            var hostOption = app.Option("-h|--host", "host", CommandOptionType.SingleValue);
            var portArgument = app.Option("-p|--port", "port", CommandOptionType.SingleValue);

            app.OnExecute(() => {
                var host = hostOption.Value() ?? "127.0.0.1";
                var portString = portArgument.Value() ?? "12000";
                int port = 12000;
               
                if (!string.IsNullOrWhiteSpace(portString))
                {
                    int.TryParse(portString, out port);
                }

                Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                Remote.Start(host, port);
                var props = Props.FromProducer(() => new EchoActor(host, port));
                Remote.RegisterKnownKind("EchoActor", props);
                context.SpawnNamed(props, "EchoActorInstance");
                Console.ReadLine();
                return 0;
            });

            app.Execute(args);
        }
    }

    public class EchoActor : IActor
    {
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
                case Ping ping:
                    context.Respond(new Pong{Message= $"{_host}:{_port} {ping.Message}"});
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }
}
