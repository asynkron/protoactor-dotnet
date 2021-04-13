// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;

namespace ContextDecorators
{
    public class LoggingRootDecorator : RootContextDecorator
    {
        public LoggingRootDecorator(IRootContext context) : base(context)
        {
        }

        public override async Task<T> RequestAsync<T>(PID target, object message, CancellationToken ct)
        {
            Console.WriteLine("Enter RequestAsync");
            var res = await base.RequestAsync<T>(target, message, ct);
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

    class Program
    {
        private static void Main(string[] args)
        {
            var context = new LoggingRootDecorator(new RootContext(new ActorSystem()));
            var props = Props.FromFunc(ctx => {
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

            var pid = context.Spawn(props);
            var res = context.RequestAsync<string>(pid, "Hello").Result;
            Console.WriteLine("Got result " + res);
            Console.ReadLine();
        }
    }
}