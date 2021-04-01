﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

namespace ContextDecorators
{
    public class LoggingRootDecorator : RootContextDecorator
    {
        public LoggingRootDecorator(IRootContext context) : base(context)
        {
        }

        public override async Task<T> RequestAsync<T>(PID target, object message)
        {
            Console.WriteLine("Enter RequestAsync");
            T res = await base.RequestAsync<T>(target, message);
            Console.WriteLine("Exit RequestAsync");
            return res;
        }
    }

    public class LoggingDecorator : ActorContextDecorator
    {
        private readonly string _loggerName;

        public LoggingDecorator(IContext context, string loggerName) : base(context) => _loggerName = loggerName;

        //we are just logging this single method
        public override void Respond(object message)
        {
            Console.WriteLine($"{_loggerName} : Enter Respond");
            base.Respond(message);
            Console.WriteLine($"{_loggerName} : Exit Respond");
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            LoggingRootDecorator context = new LoggingRootDecorator(new RootContext(new ActorSystem()));
            Props props = Props.FromFunc(ctx =>
                        {
                            if (ctx.Message is string str)
                            {
                                Console.WriteLine("Inside Actor: " + str);
                                ctx.Respond("Yo!");
                            }

                            return Task.CompletedTask;
                        }
                    )
                    .WithContextDecorator(c => new LoggingDecorator(c, "logger1"))
                    .WithContextDecorator(c => new LoggingDecorator(c, "logger2"))
                ;

            PID pid = context.Spawn(props);
            string res = context.RequestAsync<string>(pid, "Hello").Result;
            Console.WriteLine("Got result " + res);
            Console.ReadLine();
        }
    }
}
