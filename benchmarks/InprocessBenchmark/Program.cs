// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;

public class Program
{
    private static async Task Main()
    {
        var context = new RootContext(new ActorSystem());
        Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");
        const int messageCount = 1_000_000;
        const int batchSize = 100;

        Console.WriteLine("ClientCount\t\tDispatcher\t\tElapsed\t\tMsg/sec");
        var tps = new[] {50, 100, 200, 400, 800};
        int[] clientCounts = {4, 8, 16, 32};

        foreach (var t in tps)
        {
            var d = new ThreadPoolDispatcher {Throughput = t};

            foreach (var clientCount in clientCounts)
            {
                var pingActor = new PID[clientCount];
                var pongActor = new PID[clientCount];
                var completions = new TaskCompletionSource<bool>[clientCount];

                var pongProps = Props.FromProducer(() => new PongActor())
                    .WithDispatcher(d);

                for (var i = 0; i < clientCount; i++)
                {
                    var tsc = new TaskCompletionSource<bool>();
                    completions[i] = tsc;
                    var pingProps = Props.FromProducer(() => new PingActor(tsc, messageCount, batchSize))
                        .WithDispatcher(d);

                    pingActor[i] = context.Spawn(pingProps);
                    pongActor[i] = context.Spawn(pongProps);
                }

                var tasks = completions.Select(tsc => tsc.Task).ToArray();
                var sw = Stopwatch.StartNew();

                for (var i = 0; i < clientCount; i++)
                {
                    var client = pingActor[i];
                    var echo = pongActor[i];

                    context.Send(client, new Start(echo));
                }

                await Task.WhenAll(tasks);

                sw.Stop();
                var totalMessages = messageCount * 2 * clientCount;

                var x = ((int) (totalMessages / (double) sw.ElapsedMilliseconds * 1000.0d)).ToString("#,##0,,M",
                    CultureInfo.InvariantCulture
                );
                Console.WriteLine($"{clientCount}\t\t\t{t}\t\t\t{sw.ElapsedMilliseconds} ms\t\t{x}");
                await Task.Delay(2000);
            }
        }
    }
}

public class Msg
{
    public Msg(PID pingActor) => PingActor = pingActor;

    public PID PingActor { get; }
}

public class Start
{
    public Start(PID sender) => Sender = sender;

    public PID Sender { get; }
}

public class PongActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Msg msg:
                context.Send(msg.PingActor, msg);
                break;
        }

        return Task.CompletedTask;
    }
}

public class PingActor : IActor
{
    private readonly int _batchSize;
    private readonly TaskCompletionSource<bool> _wgStop;

    private int _messageCount;
    private PID _targetPid;

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
                _targetPid = s.Sender;
                SendBatch(context);
                break;
            case Msg m:
                _messageCount--;

                if (_messageCount <= 0) _wgStop.SetResult(true);
                else context.Send(_targetPid, m);
                break;
        }

        return Task.CompletedTask;
    }

    private void SendBatch(IContext context)
    {
        var m = new Msg(context.Self);

        for (var i = 0; i < _batchSize; i++)
        {
            context.Send(_targetPid, m);
        }

        _messageCount -= _batchSize;
    }
}
