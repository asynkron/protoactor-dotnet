// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        var props = Actor.FromFunc(async ctx =>
        {
            if (ctx.Message is string)
            {
                await ctx.RespondAsync("hey");
            }
        });
        var pid = Actor.Spawn(props);

        var reply = pid.RequestAsync<object>("hello").Result;
        Console.WriteLine(reply);
    }
}