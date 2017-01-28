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
                    if (_count % 50000 == 0)
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

        var messageCount = 1000000;
        var wg = new AutoResetEvent(false);
        var props = Actor
            .FromProducer(() => new LocalActor(0, messageCount, wg));

        var pid = Actor.Spawn(props);
        var remote = new PID("127.0.0.1:8080", "remote");
        remote.RequestAsync<Messages.Start>(new Messages.StartRemote() {Sender = pid}).Wait();

        var start = DateTime.Now;
        Console.WriteLine("Starting to send");
        var msg = new Messages.Ping();
        for (int i = 0; i < messageCount; i++)
        {
            remote.Tell(msg);
        }
        wg.WaitOne();
        var elapsed = DateTime.Now - start;
        Console.WriteLine("Elapsed {0}",elapsed);

        var t = ((messageCount * 2.0) / elapsed.TotalMilliseconds) * 1000;
        Console.WriteLine("Throughput {0} msg / sec",t);

        Console.ReadLine();
    }
}