// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

namespace HelloWorld;

internal class Program
{
    private static void Main(string[] args)
    {
        var system = new ActorSystem();
        var props = Props.FromProducer(() => new HelloActor());
        var pid = system.Root.Spawn(props);
        system.Root.Send(pid, new Hello("ProtoActor"));
        Console.ReadLine();
    }

    //Messages should be immutable to prevent race conditions between multiple actors
    private class Hello
    {
        public Hello(string who)
        {
            Who = who;
        }

        public string Who { get; }
    }

    //This is a standard actor
    private class HelloActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;

            if (msg is Hello r)
            {
                Console.WriteLine($"Hello {r.Who}");
            }

            return Task.CompletedTask;
        }
    }
}