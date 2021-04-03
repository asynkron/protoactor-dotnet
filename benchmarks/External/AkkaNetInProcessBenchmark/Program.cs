using Akka.Actor;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;

namespace LocalPingPong
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            var system = ActorSystem.Create("banana");
            Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");
            const int messageCount = 1_000_000;
            const int batchSize = 100;

            Console.WriteLine("ClientCount\t\tDispatcher\t\tElapsed\t\tMsg/sec");
            var tps = new[] {50, 100, 200, 400, 800};
            int[] clientCounts = {4, 8, 16, 32};


            foreach (var clientCount in clientCounts)
            {
                var pingActor = new IActorRef[clientCount];
                var pongActor = new IActorRef[clientCount];
                var completions = new TaskCompletionSource<bool>[clientCount];

                var pongProps = Props.Create(() => new PongActor());

                for (var i = 0; i < clientCount; i++)
                {
                    var tsc = new TaskCompletionSource<bool>();
                    completions[i] = tsc;
                    var pingProps = Props.Create(() => new PingActor(tsc, messageCount, batchSize));

                    pingActor[i] = system.ActorOf(pingProps);
                    pongActor[i] = system.ActorOf(pongProps);
                }

                var tasks = completions.Select(tsc => tsc.Task).ToArray();
                var sw = Stopwatch.StartNew();

                for (var i = 0; i < clientCount; i++)
                {
                    var client = pingActor[i];
                    var echo = pongActor[i];

                    client.Tell(new Start(echo));
                }

                await Task.WhenAll(tasks);

                sw.Stop();
                var totalMessages = messageCount * 2 * clientCount;

                var x = ((int) (totalMessages / (double) sw.ElapsedMilliseconds * 1000.0d)).ToString("#,##0,,M",
                    CultureInfo.InvariantCulture
                );
                Console.WriteLine($"{clientCount}\t\t\taaa\t\t\t{sw.ElapsedMilliseconds} ms\t\t{x}");
                await Task.Delay(2000);
            }
        }

    }

    
    
    public class Msg
    {
        public Msg(IActorRef pingActor) => PingActor = pingActor;

        public IActorRef PingActor { get; }
    }

    public class Start
    {
        public Start(IActorRef sender) => Sender = sender;

        public IActorRef Sender { get; }
    }

    public class PongActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Msg msg:
                    msg.PingActor.Tell(msg);
                    break;
            }
        }

    }

    public class PingActor : UntypedActor
    {
        private readonly int _batchSize;
        private readonly TaskCompletionSource<bool> _wgStop;

        private int _messageCount;
        private IActorRef _targetPid;

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Start s:
                    _targetPid = s.Sender;
                    SendBatch();
                    break;
                case Msg m:
                    _messageCount--;

                    if (_messageCount <= 0) _wgStop.TrySetResult(true);
                    else _targetPid.Tell(m);
                    break;
            }
        }

        public PingActor(TaskCompletionSource<bool> wgStop, int messageCount, int batchSize)
        {
            _wgStop = wgStop;
            _messageCount = messageCount;
            _batchSize = batchSize;
        }


        private void SendBatch()
        {
            var m = new Msg(Context.Self);

            for (var i = 0; i < _batchSize; i++)
            {
                _targetPid.Tell(m);
            }

            _messageCount -= _batchSize;
        }
    }
}