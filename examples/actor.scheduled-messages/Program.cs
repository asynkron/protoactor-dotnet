// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Timers;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var system = new ActorSystem();
        var context = new RootContext(system);

        var props = Props.FromFunc(ctx =>
            {
                if (ctx.Message is string msg)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} Received: {msg}");
                }

                return Task.CompletedTask;
            }
        );

        var pid = context.Spawn(props);
        var scheduler = context.Scheduler();

        var once = scheduler.SendOnce(TimeSpan.FromSeconds(1), pid, "once");
        var repeated = scheduler.SendRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), pid, "repeat");

        Console.WriteLine("Press [enter] to cancel scheduled messages");
        Console.ReadLine();

        once.Cancel();
        repeated.Cancel();
        once.Dispose();
        repeated.Dispose();

        Console.WriteLine("Scheduled messages cancelled. Press [enter] to exit");
        Console.ReadLine();
    }
}
