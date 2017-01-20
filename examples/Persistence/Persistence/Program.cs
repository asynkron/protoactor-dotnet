using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Persistence;

namespace PersistenceExample
{
    class Program
    {
        class MyActor : IPersistentActor
        {
            public Persistence Persistence { get; set; }

            private State _state = new State();

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Add add:
                        _state.Sum += add.Number;
                        Persistence.PersistSnapshot(add);
                        break;
                    case RequestSnapshot rs:
                        Persistence.PersistSnapshot(_state);
                        break;
                    case State s:
                        _state = s;
                        break;
                }
                return Actor.Done;
            }
        }
        
        static void Main(string[] args)
        {
            var inMemoryPersistenceProvider = new InMemoryProvider();
            var props = Actor.FromProducer(() => new MyActor())
                .WithReceivers(Persistence.Using(inMemoryPersistenceProvider));
            var pid = Actor.Spawn(props);
            pid.Tell(1);
            pid.Tell(2);
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}