using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Schedulers.SimpleScheduler
{
    public class SimpleScheduler : ISimpleScheduler
    {
        private readonly ISenderContext _context;

        public SimpleScheduler()
        {
            _context = RootContext.Empty;
        }

        public SimpleScheduler(ISenderContext context)
        {
            _context = context;
        }
        
        public ISimpleScheduler ScheduleTellOnce(TimeSpan delay, PID target, object message)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);

                _context.Send(target, message);
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

                        _context.Send(target, message);
                        
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

                //TODO: allow custom sender
                _context.Request(target, message);
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

                        //TODO: allow using sender
                        _context.Request(target,message);                       

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
