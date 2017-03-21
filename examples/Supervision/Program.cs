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
using Microsoft.Extensions.Logging;

class Program
{
    static void Main(string[] args)
    {
        Proto.Log.SetLoggerFactory(new LoggerFactory()
            .AddConsole(minLevel: LogLevel.Debug));

        var props = Actor.FromProducer(() => new ParentActor()).WithSupervisor(new OneForOneStrategy(Decider.Decide, 1, null));

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
        actor.Tell(new Recoverable());
        actor.Tell(new Fatal());

        Console.ReadLine();
    }

    internal class Decider
    {
        public static SupervisorDirective Decide(PID pid, Exception reason)
        {
            switch (reason)
            {
                case RecoverableException r:
                    return SupervisorDirective.Restart;
                case FatalException r:
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
                var props = Actor.FromProducer(() => new ChildActor());
                child = context.Spawn(props);
            }
            else
            {
                child = context.Children.First();
            }

            switch (context.Message)
            {
                case Hello r:
                    child.Tell(context.Message);
                    break;
                case Recoverable r:
                    child.Tell(context.Message);
                    break;
                case Fatal r:
                    child.Tell(context.Message);
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
            var msg = context.Message;

            switch (context.Message)
            {
                case Hello r:
                    logger.LogDebug($"Hello {r.Who}");
                    break;
                case Recoverable r:
                    throw new RecoverableException();
                case Fatal r:
                    throw new FatalException();
                case Started r:
                    logger.LogDebug("Started, initialize actor here");
                    break;
                case Stopping r:
                    logger.LogDebug("Stopping, actor is about shut down");
                    break;
                case Stopped r:
                    logger.LogDebug("Stopped, actor and it's children are stopped");
                    break;
                case Restarting r:
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