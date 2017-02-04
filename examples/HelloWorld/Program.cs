using System;
using System.Threading.Tasks;
using Proto;

class Program
{
    internal class Hello
    {
        public string Who;
    }

    internal class HelloActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;
            if (msg is Hello r)
            {
                Console.WriteLine($"Hello {r.Who}");
            }
            return Actor.Done;
        }
    }

    static void Main(string[] args)
    {
        var props = Actor.FromProducer(() => new HelloActor());
        var pid = Actor.Spawn(props);
        pid.Tell(new Hello
        {
            Who = "Alex"
        });
        Console.ReadLine();
    }
}