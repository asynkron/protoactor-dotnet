using System;
using Proto;

namespace ContextDecorators
{
    public class LoggingDecorator : ActorContextProxy
    {
        public LoggingDecorator(IContext context) : base(context)
        {
        }

        //we are just logging this single method
        public override void Respond(object message)
        {
            Console.WriteLine("Enter Respond");
            base.Respond(message);
            Console.WriteLine("Exit Respond");
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var context = new RootContext();
            var props = Props.FromFunc(ctx =>
            {
                if (ctx.Message is string str)
                {
                    Console.WriteLine("Inside Actor: " + str);
                    ctx.Respond("Yo!");
                }

                return Actor.Done;
            }).WithContextDecorator(c => new LoggingDecorator(c));
            var pid = context.Spawn(props);
            var res = context.RequestAsync<string>(pid, "Hello").Result;
            Console.WriteLine("Got result " + res);
            Console.ReadLine();
        }
    }
}