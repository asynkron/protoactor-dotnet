// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

namespace Futures
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            RootContext context = new RootContext(new ActorSystem());
            Props props = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string)
                    {
                        ctx.Respond("hey");
                    }

                    return Task.CompletedTask;
                }
            );
            PID pid = context.Spawn(props);

            object reply = await context.RequestAsync<object>(pid, "hello");
            Console.WriteLine(reply);
            Console.ReadLine();
        }
    }
}
