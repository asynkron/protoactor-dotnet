using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Proto;

namespace Saga
{
    internal class Program
    {
        public static readonly RootContext Context = new RootContext();

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting");
            var random = new Random();
            var numberOfTransfers = 1000;
            var uptime = 99.99;
            var retryAttempts = 0;
            var refusalProbability = 0.01;
            var busyProbability = 0.01;
            var provider = new InMemoryProvider();

            var props = Props.FromProducer(() => new Runner(numberOfTransfers, uptime, refusalProbability, busyProbability, retryAttempts, false))
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, retryAttempts, null));
            
            Console.WriteLine("Spawning runner");
            var runner = Context.SpawnNamed(props, "runner");
           
            Console.ReadLine();
        }
    }
}
