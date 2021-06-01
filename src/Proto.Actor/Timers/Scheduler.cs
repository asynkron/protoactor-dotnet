using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Timers
{
    [PublicAPI]
    public class Scheduler
    {
        private readonly ISenderContext _context;

        public Scheduler(ISenderContext context) => _context = context;

        public CancellationTokenSource SendOnce(TimeSpan delay, PID target, object message)
        {
            var cts = new CancellationTokenSource();

            _ = SafeTask.Run(async () => {
                    await Task.Delay(delay, cts.Token);

                    _context.Send(target, message);
                }, cts.Token
            );

            return cts;
        }

        public CancellationTokenSource SendRepeatedly(TimeSpan interval, PID target, object message) =>
            SendRepeatedly(interval, interval, target, message);

        public CancellationTokenSource SendRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message)
        {
            var cts = new CancellationTokenSource();

            _ = SafeTask.Run(async () => {
                    await Task.Delay(delay, cts.Token);

                    async Task Trigger()
                    {
                        while (true)
                        {
                            if (cts.IsCancellationRequested)
                                return;

                            _context.Send(target, message);

                            await Task.Delay(interval, cts.Token);
                        }
                    }

                    await Trigger();
                }, cts.Token
            );

            return cts;
        }

        public CancellationTokenSource RequestRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message)
        {
            var cts = new CancellationTokenSource();

            _ = SafeTask.Run(async () => {
                    await Task.Delay(delay, cts.Token);

                    async Task Trigger()
                    {
                        while (true)
                        {
                            if (cts.IsCancellationRequested)
                                return;

                            _context.Request(target, message);

                            await Task.Delay(interval, cts.Token);
                        }
                    }

                    await Trigger();
                }, cts.Token
            );

            return cts;
        }
    }
}