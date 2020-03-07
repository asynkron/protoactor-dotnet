using System.Threading.Tasks;

namespace Proto.Actor.Benchmarks
{
    public class PingActor : IActor
    {
        private readonly int _batchSize;
        private readonly TaskCompletionSource<bool> _wgStop;
        private int _batch;
        private int _messageCount;

        public PingActor(TaskCompletionSource<bool> wgStop, int messageCount, int batchSize)
        {
            _wgStop = wgStop;
            _messageCount = messageCount;
            _batchSize = batchSize;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Start s:
                    SendBatch(context, s.Sender);
                    break;
                case Msg m:
                    _batch--;

                    if (_batch > 0)
                    {
                        break;
                    }

                    if (!SendBatch(context, m.Sender))
                    {
                        _wgStop.SetResult(true);
                    }

                    break;
            }

            return Task.CompletedTask;
        }

        private bool SendBatch(IContext context, PID sender)
        {
            if (_messageCount == 0)
            {
                return false;
            }

            var m = new Msg(context.Self);

            for (var i = 0; i < _batchSize; i++)
            {
                context.Send(sender, m);
            }

            _messageCount -= _batchSize;
            _batch = _batchSize;
            return true;
        }
    }

    public class EchoActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Msg msg:
                    context.Send(msg.Sender, msg);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    public class EchoActor2 : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case string _:
                    context.Send(context.Sender, "pong");
                    break;
            }

            return Task.CompletedTask;
        }
    }

    public class Start
    {
        public Start(PID sender) => Sender = sender;

        public PID Sender { get; }
    }

    public class Msg
    {
        public Msg(PID sender) => Sender = sender;

        public PID Sender { get; }
    }
}