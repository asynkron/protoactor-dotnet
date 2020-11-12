// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

namespace LifecycleEvents
{
    internal static class Program
    {
        private static async Task Main()
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
            
            //StopAsync. Stop instantly kills actor
            //Poison lets it process any waiting messages first
            await system.Root.PoisonAsync(actor);
        }

        private class ChildActor : IActor
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

                return Task.CompletedTask;
            }
        }

        private class Hello
        {
            public string Who;
        }
    }
}
