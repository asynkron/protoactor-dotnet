using System;
using Proto;

namespace EscalateSupervision
{
    class Program
    {
        static void Main(string[] args)
        {
            var childProps = Props.FromFunc(context =>
            {
                Console.WriteLine($"{context.Self.Id}: MSG: {context.Message.GetType()}");
                switch (context.Message)
                {
                    case Started _:
                        throw new Exception("child failure");
                }
                return Actor.Done;
            });

            var rootProps = Props.FromFunc(context =>
            {
                Console.WriteLine($"{context.Self.Id}: MSG: {context.Message.GetType()}");
                switch (context.Message)
                {
                    case Started _:
                        context.SpawnNamed(childProps, "child");
                        break;
                    case Terminated terminated:
                        Console.WriteLine($"Terminated {terminated.Who}");
                        break;
                }
                return Actor.Done;
            })
            .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 0, null));

            var rootContext = new RootContext();
            rootContext.SpawnNamed(rootProps, "root");

            Console.ReadLine();
        }
    }
}
