using Proto;
using Proto.Schedulers.SimpleScheduler;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleSchedulerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var props = Actor.FromProducer(() => new ScheduleActor());

            var pid = Actor.Spawn(props);

            Console.ReadLine();
        }
    }

    public class Hello { }
    public class HickUp { }
    public class AbortHickUp { }
    public class Greet
    {
        public Greet(string who)
        {
            Who = who;
        }

        public string Who { get; }
    }
    public class SimpleMessage
    {
        public SimpleMessage(string msg)
        {
            Msg = msg;
        }

        public string Msg { get; }
    }

    public class ScheduleActor : IActor
    {
        private ISimpleScheduler scheduler = new SimpleScheduler();

        private CancellationTokenSource timer;

        private int counter = 0;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:

                    var pid = context.Spawn(Actor.FromProducer(() => new ScheduleGreetActor()));
                    
                    scheduler
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(100), context.Self, new SimpleMessage("test 1"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(200), context.Self, new SimpleMessage("test 2"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(300), context.Self, new SimpleMessage("test 3"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(400), context.Self, new SimpleMessage("test 4"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(500), context.Self, new SimpleMessage("test 5"))
                        .ScheduleRequestOnce(TimeSpan.FromSeconds(1), context.Self, pid, new Greet("Daniel"))
                        .ScheduleTellOnce(TimeSpan.FromSeconds(5), context.Self, new Hello());

                    break;

                case Hello hl:

                    Console.WriteLine($"Hello Once, let's give you a hickup every 0.5 second starting in 3 seconds!");

                    scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(500), context.Self, new HickUp(), out timer);

                    break;

                case HickUp hu:

                    counter++;

                    Console.WriteLine($"Hello!");

                    if (counter == 5)
                    {
                        timer.Cancel();

                        return context.Self.SendAsync(new AbortHickUp());
                    }

                    break;

                case AbortHickUp ahu:

                    Console.WriteLine($"Aborted hickup after {counter} times");

                    Console.WriteLine("All this was scheduled calls, have fun!");

                    break;

                case Greet msg:

                    Console.WriteLine($"Thanks {msg.Who}");

                    break;

                case SimpleMessage sm:

                    Console.WriteLine(sm.Msg);

                    break;
            }

            return Actor.Done;
        }
    }

    public class ScheduleGreetActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch(context.Message)
            {
                case Greet msg:

                    Console.WriteLine($"Hi {msg.Who}!");

                    return context.Sender.SendAsync(new Greet("Roger"));
            }

            return Actor.Done;
        }
    }
}