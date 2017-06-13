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

                await target.SendAsync(message);
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

                        await target.SendAsync(message);

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

                await target.RequestAsync(message, sender);
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

                    await target.RequestAsync(message, sender);

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
