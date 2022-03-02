using System;
using System.Threading.Tasks;
using Proto;

namespace LocalPingPong;

public record PingMsg(PID Sender);

public record PongMsg;

public class PingActor : IActor
{
    private readonly int _batchSize;
    private int _messageCount;
    private PID _pong;
    private PID _replyTo;

    public PingActor(int messageCount, int batchSize)
    {
        _messageCount = messageCount;
        _batchSize = batchSize;
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Start s:
                _pong = s.Sender;
                _replyTo = context.Sender;
                var m = new PingMsg(context.Self);

                _messageCount -= _batchSize;

                for (var i = 0; i < _batchSize; i++)
                {
                    context.Send(_pong, m);
                }

                break;
            case PongMsg:

                _messageCount--;

                if (_messageCount == 0)
                {
                    Console.Write(".");
                    context.Send(_replyTo, true);
                }
                else if (_messageCount > 0) context.Send(_pong, new PingMsg(context.Self));

                break;
        }

        return Task.CompletedTask;
    }

    public static Props Props(int messageCount, int batchSize) =>
        Proto.Props.FromProducer(() => new PingActor(messageCount, batchSize));

    public class Start
    {
        public Start(PID sender) => Sender = sender;

        public PID Sender { get; }
    }
}