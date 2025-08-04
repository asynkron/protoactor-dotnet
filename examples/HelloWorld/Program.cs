// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

var system = new ActorSystem();
var props = Props.FromProducer(() => new HelloActor());
var pid = system.Root.Spawn(props);
system.Root.Send(pid, new Hello("ProtoActor"));
Console.ReadLine();

// Messages should be immutable to prevent race conditions between multiple actors
internal record Hello(string Who);

// This is a standard actor
internal class HelloActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is Hello r)
        {
            Console.WriteLine($"Hello {r.Who}");
        }

        return Task.CompletedTask;
    }
}
