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
    class Program
    {
        private static async Task Main(string[] args)
        {
            var context = new RootContext(new ActorSystem());
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is string) ctx.Respond("hey");
                    return Task.CompletedTask;
                }
            );
            var pid = context.Spawn(props);

            var reply = await context.RequestAsync<object>(pid, "hello");
            Console.WriteLine(reply);
            Console.ReadLine();
        }
    }
}