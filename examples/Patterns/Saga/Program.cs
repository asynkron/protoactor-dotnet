// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Proto;

namespace Saga
{
    internal class Program
    {
        private static readonly RootContext Context = new ActorSystem().Root;

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting");
            Random random = new Random();
            int numberOfTransfers = 5;
            int intervalBetweenConsoleUpdates = 1;
            double uptime = 99.99;
            int retryAttempts = 0;
            double refusalProbability = 0.01;
            double busyProbability = 0.01;
            bool verbose = false;

            Props props = Props.FromProducer(() => new Runner(numberOfTransfers, intervalBetweenConsoleUpdates, uptime,
                        refusalProbability, busyProbability, retryAttempts, verbose
                    )
                )
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart,
                        retryAttempts, null
                    )
                );

            Console.WriteLine("Spawning runner");
            PID runner = Context.SpawnNamed(props, "runner");

            Console.ReadLine();
        }
    }
}
