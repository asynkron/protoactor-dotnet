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
            var context = new RootContext();
            var props = Props.FromProducer(() => new ScheduleActor());

            var pid = context.Spawn(props);

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
        private readonly ISimpleScheduler _scheduler = new SimpleScheduler();

        private CancellationTokenSource _timer;

        private int _counter;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:

                    var pid = context.Spawn(Props.FromProducer(() => new ScheduleGreetActor()));
                    
                    _scheduler
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(100), context.Self, new SimpleMessage("test 1"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(200), context.Self, new SimpleMessage("test 2"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(300), context.Self, new SimpleMessage("test 3"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(400), context.Self, new SimpleMessage("test 4"))
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(500), context.Self, new SimpleMessage("test 5"))
                        .ScheduleRequestOnce(TimeSpan.FromSeconds(1), context.Self, pid, new Greet("Daniel"))
                        .ScheduleTellOnce(TimeSpan.FromSeconds(5), context.Self, new Hello());

                    break;

                case Hello _:

                    Console.WriteLine($"Hello Once, let's give you a hickup every 0.5 second starting in 3 seconds!");

                    _scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(500), context.Self, new HickUp(), out _timer);

                    break;

                case HickUp _:

                    _counter++;

                    Console.WriteLine("Hello!");

                    if (_counter == 5)
                    {
                        _timer.Cancel();

                        context.Send(context.Self, new AbortHickUp());
                    }

                    break;

                case AbortHickUp _:

                    Console.WriteLine($"Aborted hickup after {_counter} times");

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

                    context.Respond(new Greet("Roger"));

                    break;
            }

            return Actor.Done;
        }
    }
}