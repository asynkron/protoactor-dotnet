// -----------------------------------------------------------------------
// <copyright file="MailboxBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Mailbox;

[SimpleJob(1, 3, 5, 100, "QuickJob"), ShortRunJob]
public class MailboxBenchmark
{
    [Benchmark]
    public Task Unbounded() => RunTest(() => UnboundedMailbox.Create());

    [Benchmark]
    public Task Unbound() => RunTest(() => BoundedMailbox.Create(1024 * 1024));

    private static async Task RunTest(MailboxProducer mailbox)
    {
        var context = new RootContext(new ActorSystem());
        const int n = 10 * 1000;
        var props = Props.FromFunc(c => {
                    switch (c.Message)
                    {
                        case string s:
                            c.Respond("done");
                            break;
                    }

                    return Task.CompletedTask;
                }
            )
            .WithMailbox(mailbox);
        var pid = context.Spawn(props);

        for (var i = 1; i <= n; i++)
        {
            context.Send(pid, i);
        }

        await context.RequestAsync<string>(pid, "stop");
    }
}