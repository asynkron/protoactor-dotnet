// -----------------------------------------------------------------------
//  <copyright file="MailboxBenchmark.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes.Jobs;

[SimpleJob(launchCount: 1, warmupCount: 3, targetCount: 5, invocationCount: 100, id: "QuickJob")]
[ShortRunJob]
public class MailboxBenchmark
{

    [Benchmark]
    public Task Unbounded() => RunTest(() => UnboundedMailbox.Create());

    [Benchmark]
    public Task Unbound() => RunTest(() => BoundedMailbox.Create(1024 * 1024));


    public static async Task RunTest(Func<IMailbox> mailbox)
    {
        const int n = 10 * 1000;
        var props = Actor.FromFunc(c =>
            {
                switch (c.Message)
                {
                    case string s:
                        c.Respond("done");
                        break;
                }
                return Actor.Done;
            })
            .WithMailbox(mailbox);
        var pid = Actor.Spawn(props);
        for (var i = 1; i <= n; i++)
        {
            pid.Tell(i);
        }
        await pid.RequestAsync<string>("stop");
    }
}
