using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public static class Scheduler
    {
        public static void ScheduleTellOnce(this IContext context, TimeSpan delay, PID target, object message)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);

                context.Tell(target, message);
            });
        }

        public static CancellationTokenSource ScheduleTellRepeatedly(this IContext context, TimeSpan delay, TimeSpan interval, PID target, object message)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                await Task.Delay(delay);

                async void Trigger(object _message)
                {
                    if (cts.IsCancellationRequested)
                        return;

                    context.Tell(target, message);

                    await Task.Delay(interval);

                    Trigger(_message);
                }

                Trigger(message);

            }, cts.Token);

            return cts;
        }
    }
}
