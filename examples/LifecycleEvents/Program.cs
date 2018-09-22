// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jaeger;
using Jaeger.Samplers;
using OpenTracing.Util;
using Proto;
using Proto.OpenTracing;

class Program
{
    static void Main(string[] args)
    {
        var tracer = new Tracer.Builder("Proto.Example.LifecycleEvents")
            .WithSampler(new ConstSampler(true))
            .Build();
        GlobalTracer.Register(tracer);

        EventStream.Instance.Subscribe<DeadLetterEvent>(
            dl =>
            {
                Console.WriteLine($"DeadLetter from {dl.Sender} to {dl.Pid} : {dl.Message?.GetType().Name} = '{dl.Message?.ToString()}'");
            });

        var context = new RootContext(new MessageHeader(), OpenTracingExtensions.OpenTracingSenderMiddleware());

        var props = Props
            .FromProducer(() => new ChildActor())
            .WithOpenTracing();

        var actor = context.Spawn(props);
        context.Send(actor, new Hello
        {
            Who = "Alex"
        });

        //why wait?
        //Stop is a system message and is not processed through the user message mailbox
        //thus, it will be handled _before_ any user message
        //we only do this to show the correct order of events in the console
        Thread.Sleep(TimeSpan.FromSeconds(1));
        actor.StopAsync().Wait();

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

    internal class Hello
    {
        public string Who;
    }
}