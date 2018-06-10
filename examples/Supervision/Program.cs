// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Microsoft.Extensions.Logging;

class Program
{
    static void Main(string[] args)
    {
        var context = new RootContext();
        Log.SetLoggerFactory(new LoggerFactory()
            .AddConsole(LogLevel.Debug));

        var props = Props.FromProducer(() => new ParentActor()).WithChildSupervisorStrategy(new OneForOneStrategy(Decider.Decide, 1, null));

        var actor = context.Spawn(props);
        
        context.Send(actor,new Hello
        {
            Who = "Alex"
        });
        context.Send(actor,new Recoverable());
        context.Send(actor,new Fatal());
        //why wait?
        //Stop is a system message and is not processed through the user message mailbox
        //thus, it will be handled _before_ any user message
        //we only do this to show the correct order of events in the console
        Thread.Sleep(TimeSpan.FromSeconds(1));
        actor.Stop();
        Console.ReadLine();
    }

    internal class Decider
    {
        public static SupervisorDirective Decide(PID pid, Exception reason)
        {
            switch (reason)
            {
                case RecoverableException _:
                    return SupervisorDirective.Restart;
                case FatalException _:
                    return SupervisorDirective.Stop;
                default:
                    return SupervisorDirective.Escalate;

            }
        }
    }

    internal class ParentActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            PID child;
            if (context.Children == null || context.Children.Count == 0)
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

            return Actor.Done;
        }
    }

    internal class ChildActor : IActor
    {
        private ILogger logger = Log.CreateLogger<ChildActor>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Hello r:
                    logger.LogDebug($"Hello {r.Who}");
                    break;
                case Recoverable _:
                    throw new RecoverableException();
                case Fatal _:
                    throw new FatalException();
                case Started _:
                    logger.LogDebug("Started, initialize actor here");
                    break;
                case Stopping _:
                    logger.LogDebug("Stopping, actor is about shut down");
                    break;
                case Stopped _:
                    logger.LogDebug("Stopped, actor and it's children are stopped");
                    break;
                case Restarting _:
                    logger.LogDebug("Restarting, actor is about restart");
                    break;
            }
            return Actor.Done;
        }
    }

    internal class Hello
    {
        public string Who;
    }
    internal class RecoverableException : Exception { }
    internal class FatalException : Exception { }
    internal class Fatal { }
    internal class Recoverable { }
}