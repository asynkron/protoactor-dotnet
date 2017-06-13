// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        var props = Actor.FromProducer(() => new HelloActor());
        var pid = Actor.Spawn(props);
        pid.SendAsync(new Hello
        {
            Who = "ProtoActor"
        }).Wait();
        Console.ReadLine();
    }

    internal class Hello
    {
        public string Who;
    }

    internal class HelloActor : IActor
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