// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;

namespace LifecycleEvents
{
    static class Program
    {
        private static void Main()
        {
            var system = new ActorSystem();

            system.EventStream.Subscribe<DeadLetterEvent>(
                dl => Console.WriteLine($"DeadLetter from {dl.Sender} to {dl.Pid} : {dl.Message.GetType().Name} = '{dl.Message}'")
            );

            var context = new RootContext(system);

            var props = Props.FromProducer(() => new ChildActor());

            var actor = context.Spawn(props);

            context.Send(
                actor, new Hello
                {
                    Who = "Alex"
                }
            );

            //why wait?
            //Stop is a system message and is not processed through the user message mailbox
            //thus, it will be handled _before_ any user message
            //we only do this to show the correct order of events in the console
            Thread.Sleep(TimeSpan.FromSeconds(1));
            system.Root.StopAsync(actor).Wait();

            Console.ReadLine();
        }

        internal class ChildActor : IActor
        {
            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Hello r:
                        Console.WriteLine($"Hello {r.Who}");
                        break;
                    case Started _:
                        Console.WriteLine("Started, initialize actor here");
                        break;
                    case Stopping _:
                        Console.WriteLine("Stopping, actor is about shut down");
                        break;
                    case Stopped _:
                        Console.WriteLine("Stopped, actor and it's children are stopped");
                        break;
                    case Restarting _:
                        Console.WriteLine("Restarting, actor is about restart");
                        break;
                }

                return Actor.Done;
            }
        }

        private class Hello
        {
            public string Who;
        }
    }
}
