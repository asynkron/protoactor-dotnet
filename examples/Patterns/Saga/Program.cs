// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Proto;

namespace Saga
{
    class Program
    {
        private static readonly RootContext Context = new ActorSystem().Root;

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
            var verbose = false;

            var props = Props.FromProducer(() =>
                    new Runner(numberOfTransfers, intervalBetweenConsoleUpdates, uptime, refusalProbability, busyProbability, retryAttempts, verbose)
                )
                .WithChildSupervisorStrategy(new OneForOneStrategy((_, _) => SupervisorDirective.Restart,
                        retryAttempts, null
                    )
                );

            Console.WriteLine("Spawning runner");
            var runner = Context.SpawnNamed(props, "runner");

            Console.ReadLine();
        }
    }
}