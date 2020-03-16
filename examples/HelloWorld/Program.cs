// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

class Program
{
    static void Main(string[] args)
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
        public string Who { get; }

        public Hello(string who)
        {
            Who = who;
        }
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
            return Actor.Done;
        }
    }
}