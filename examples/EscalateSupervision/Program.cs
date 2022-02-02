// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto;

namespace EscalateSupervision
{
    class Program
    {
        private static void Main(string[] args)
        {
            var childProps = Props.FromFunc(context => {
                    Console.WriteLine($"{context.Self.Id}: MSG: {context.Message.GetType()}");

                    switch (context.Message)
                    {
                        case Started _:
                            throw new Exception("child failure");
                    }

                    return Task.CompletedTask;
                }
            );

            var rootProps = Props.FromFunc(context => {
                        Console.WriteLine($"{context.Self.Id}: MSG: {context.Message.GetType()}");

                        switch (context.Message)
                        {
                            case Started _:
                                context.SpawnNamed(childProps, "child");
                                break;
                            case Terminated terminated:
                                Console.WriteLine($"Terminated {terminated.Who}");
                                break;
                        }

                        return Task.CompletedTask;
                    }
                )
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 0,
                        null
                    )
                );

            var rootContext = new RootContext(new ActorSystem());
            rootContext.SpawnNamed(rootProps, "root");

            Console.ReadLine();
        }
    }
}