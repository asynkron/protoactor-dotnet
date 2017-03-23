using Proto;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scheduler
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

    public class ScheduleActor : IActor
    {
        private CancellationTokenSource timer;
        private int counter = 0;

        public Task ReceiveAsync(IContext context)
        {
            switch(context.Message)
            {
                case Started _:

                    context.ScheduleTellOnce(TimeSpan.FromSeconds(1), context.Self, new Hello());

                    timer = context.ScheduleTellRepeatedly(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250), context.Self, new HickUp());

                    break;

                case Hello hl:

                    Console.WriteLine($"Hello Once, let's give you a 5 times hickup!");

                    break;

                case HickUp hu:

                    counter++;

                    Console.WriteLine($"Hello!");

                    if (counter == 5)
                    {
                        timer.Cancel();

                        context.Self.Tell(new AbortHickUp());
                    }

                    break;

                case AbortHickUp ahu:

                    Console.WriteLine($"Aborted hickup after {counter} times");

                    break;
            }

            return Actor.Done;
        }
    }
}