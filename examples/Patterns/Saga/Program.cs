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
        private static RootContext Context = new ActorSystem().Root;

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting");
            var random = new Random();
            var numberOfTransfers = 5;
            var intervalBetweenConsoleUpdates = 1;
            var uptime = 99.99;
            var retryAttempts = 0;
            var refusalProbability = 0.01;
            var busyProbability = 0.01;
            bool verbose = false;

            var props = Props.FromProducer(() => new Runner(numberOfTransfers, intervalBetweenConsoleUpdates, uptime, refusalProbability, busyProbability, retryAttempts, verbose))
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, retryAttempts, null));

            Console.WriteLine("Spawning runner");
            var runner = Context.SpawnNamed(props, "runner");

            Console.ReadLine();
        }
    }
}
