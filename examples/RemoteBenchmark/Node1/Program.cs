using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    public class LocalActor : IActor
    {
        private int _count;
        private AutoResetEvent _wg;
        private int _messageCount;

        public LocalActor(int count, int messageCount, AutoResetEvent wg)
        {
            _count = count;
            _messageCount = messageCount;
            _wg = wg;
        }


        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Messages.Pong _:
                    _count++;
                    if (_count % 5000 == 0)
                    {
                        Console.WriteLine(_count);
                    }
                    if (_count == _messageCount)
                    {
                        _wg.Set();

                    }
                    break;

            }
            return Actor.Done;
        }
    }
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        RemotingSystem.Start("127.0.0.1", 8081);

        
        var wg = new AutoResetEvent(false);
        var props = Actor.FromProducer(() => new LocalActor(0, 1000000, wg));
        var pid = Actor.Spawn(props);
        var remote = new PID("127.0.0.1:8080", "remote");
        Console.WriteLine("Starting Remote");
        remote.RequestAsync<Messages.Start>(new Messages.StartRemote() {Sender = pid}).Wait();
        Console.WriteLine("Remote started");

        Console.ReadLine();
    }
}