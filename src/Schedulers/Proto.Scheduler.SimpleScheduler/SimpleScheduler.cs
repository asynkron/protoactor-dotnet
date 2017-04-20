using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Schedulers.SimpleScheduler
{
    public class SimpleScheduler : ISimpleScheduler
    {
        public ISimpleScheduler ScheduleTellOnce(TimeSpan delay, PID target, object message)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);

                target.Tell(message);
            });

            return this;
        }

        public ISimpleScheduler ScheduleTellRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message, out CancellationTokenSource cancellationTokenSource)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                await Task.Delay(delay);

                async void Trigger()
                {
                    while (true)
                    {
                        if (cts.IsCancellationRequested)
                            return;

                        target.Tell(message);

                        await Task.Delay(interval);
                    }
                }

                Trigger();

            }, cts.Token);

            cancellationTokenSource = cts;

            return this;
        }

        public ISimpleScheduler ScheduleRequestOnce(TimeSpan delay, PID sender, PID target, object message)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);

                target.Request(message, sender);
            });

            return this;
        }

        public ISimpleScheduler ScheduleRequestRepeatedly(TimeSpan delay, TimeSpan interval, PID sender, PID target, object message, out CancellationTokenSource cancellationTokenSource)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                await Task.Delay(delay);

                async void Trigger(object _message)
                {
                    if (cts.IsCancellationRequested)
                        return;

                    target.Request(message, sender);

                    await Task.Delay(interval);

                    Trigger(_message);
                }

                Trigger(message);

            }, cts.Token);

            cancellationTokenSource = cts;

            return this;
        }
    }
}
