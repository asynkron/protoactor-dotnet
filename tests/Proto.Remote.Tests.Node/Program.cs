using System;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = "127.0.0.1";
            var port = 12000;
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            Remote.Start(host, port);
            var props = Actor.FromProducer(() => new EchoActor(host, port));
            Remote.RegisterKnownKind("remote", props);
            Actor.SpawnNamed(props, "remote");
            Console.ReadLine();
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
                    context.Sender.Tell(new Pong{Message= $"{_host}:{_port} {ping.Message}"});
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }
}
