// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        var props = Actor.FromProducer(() => new ChildActor());
        var actor = Actor.Spawn(props);
        actor.Tell(new Hello
        {
            Who = "Alex"
        });
        //why wait?
        //Stop is a system message and is not processed through the user message mailbox
        //thus, it will be handled _before_ any user message
        //we only do this to show the correct order of events in the console
        Thread.Sleep(TimeSpan.FromSeconds(1));
        actor.Stop();

        Console.ReadLine();
    }    

    internal class ChildActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;

            switch (context.Message)
            {
                case Hello r:
                    Console.WriteLine($"Hello {r.Who}");
                    break;
                case Started r:
                    Console.WriteLine("Started, initialize actor here");
                    break;
                case Stopping r:
                    Console.WriteLine("Stopping, actor is about shut down");
                    break;
                case Stopped r:
                    Console.WriteLine("Stopped, actor and it's children are stopped");
                    break;
                case Restarting r:
                    Console.WriteLine("Restarting, actor is about restart");
                    break;
            }
            return Actor.Done;
        }
    }

    internal class Hello
    {
        public string Who;
    }
}