// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        var context = new RootContext();
        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is string)
            {
                ctx.Respond("hey");
            }
            return Actor.Done;
        });
        var pid = context.Spawn(props);

        var reply = context.RequestAsync<object>(pid ,"hello").Result;
        Console.WriteLine(reply);
        Console.ReadLine();
    }
}