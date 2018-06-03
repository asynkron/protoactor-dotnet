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

                target.Send(message);
            });

            return this;
        }

        public ISimpleScheduler ScheduleTellRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message, out CancellationTokenSource cancellationTokenSource)
        {
            var cts = new CancellationTokenSource();

            var _ = Task.Run(async () =>
            {
                await Task.Delay(delay, cts.Token);

                async void Trigger()
                {
                    while (true)
                    {
                        if (cts.IsCancellationRequested)
                            return;

                        target.Send(message);

                        await Task.Delay(interval, cts.Token);
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

            var _ = Task.Run(async () =>
            {
                await Task.Delay(delay, cts.Token);

                async void Trigger()
                {
                    while (true)
                    {
                        if (cts.IsCancellationRequested)
                            return;

                        target.Request(message, sender);

                        await Task.Delay(interval, cts.Token);
                    }
                }

                Trigger();

            }, cts.Token);

            cancellationTokenSource = cts;

            return this;
        }
    }
}
