using System;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(Tests.Messages.ProtosReflection.Descriptor);
            Remote.Start("127.0.0.1", 12000);
            var props = Actor.FromProducer(() => new EchoActor());
            Remote.RegisterKnownKind("remote", props);
            Actor.SpawnNamed(props, "remote");
            Console.ReadLine();
        }
    }

    public class EchoActor : IActor
    {
        private PID _sender;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case StartRemote sr:
                    Console.WriteLine("Starting");
                    _sender = sr.Sender;
                    context.Respond(new Start());
                    return Actor.Done;
                case Ping _:
                    _sender.Tell(new Pong());
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }
}
