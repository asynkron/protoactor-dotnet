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
            var res = await base.RequestAsync<T>(target, message);
            Console.WriteLine("Exit RequestAsync");
            return res;
        }
    }
    
    public class LoggingDecorator : ActorContextDecorator
    {
        private readonly string _loggerName;

        public LoggingDecorator(IContext context, string loggerName) : base(context)
        {
            _loggerName = loggerName;
        }

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
        static void Main(string[] args)
        {
            var context = new LoggingRootDecorator(new RootContext());
            var props = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string str)
                    {
                        Console.WriteLine("Inside Actor: " + str);
                        ctx.Respond("Yo!");
                    }

                    return Actor.Done;
                })
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