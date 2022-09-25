﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;

internal class Program
{
    private static void Main()
    {
        var context = new RootContext(new ActorSystem());
        Log.SetLoggerFactory(LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug)));

        var props = Props.FromProducer(() => new ParentActor())
            .WithChildSupervisorStrategy(new OneForOneStrategy(Decider.Decide, 1, null));

        var actor = context.Spawn(props);

        context.Send(
            actor, new Hello
            {
                Who = "Alex"
            }
        );

        context.Send(actor, new Recoverable());
        context.Send(actor, new Fatal());
        //why wait?
        //Stop is a system message and is not processed through the user message mailbox
        //thus, it will be handled _before_ any user message
        //we only do this to show the correct order of events in the console
        Thread.Sleep(TimeSpan.FromSeconds(1));
        context.Stop(actor);
        Console.ReadLine();
    }

    private static class Decider
    {
        public static SupervisorDirective Decide(PID pid, Exception reason) =>
            reason switch
            {
                RecoverableException _ => SupervisorDirective.Restart,
                FatalException _       => SupervisorDirective.Stop,
                _                      => SupervisorDirective.Escalate
            };
    }

    private class ParentActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            PID child;

            if (context.Children is null || context.Children.Count == 0)
            {
                var props = Props.FromProducer(() => new ChildActor());
                child = context.Spawn(props);
            }
            else
            {
                child = context.Children.First();
            }

            switch (context.Message)
            {
                case Hello _:
                case Recoverable _:
                case Fatal _:
                    context.Forward(child);

                    break;
                case Terminated r:
                    Console.WriteLine("Watched actor was Terminated, {0}", r.Who);

                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class ChildActor : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<ChildActor>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Hello r:
                    _logger.LogDebug($"Hello {r.Who}");

                    break;
                case Recoverable _:
                    throw new RecoverableException();
                case Fatal _:
                    throw new FatalException();
                case Started _:
                    _logger.LogDebug("Started, initialize actor here");

                    break;
                case Stopping _:
                    _logger.LogDebug("Stopping, actor is about shut down");

                    break;
                case Stopped _:
                    _logger.LogDebug("Stopped, actor and it's children are stopped");

                    break;
                case Restarting _:
                    _logger.LogDebug("Restarting, actor is about restart");

                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class Hello
    {
        public string Who;
    }

    private class RecoverableException : Exception
    {
    }

    private class FatalException : Exception
    {
    }

    private class Fatal
    {
    }

    private class Recoverable
    {
    }
}