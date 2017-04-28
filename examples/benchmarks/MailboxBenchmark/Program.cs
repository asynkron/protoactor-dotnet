// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Proto;
using Proto.Mailbox;

class Program
{
    static void Main(string[] args)
    {
        Func<IMailbox> unboundedMailbox =
            () => UnboundedMailbox.Create();
        //Func<IMailbox> boundedMailbox =
        //    () => BoundedMailbox.Create(1024 * 1024);

        //RunTest(boundedMailbox, "Bounded mailbox");
        RunTest(unboundedMailbox, "Unbounded mailbox");

        Console.ReadLine();
    }

    private static void RunTest(Func<IMailbox> mailbox, string name)
    {
        Stopwatch sendSw = new Stopwatch(), recvSw = new Stopwatch();
        const int n = 10 * 1000 * 1000;
        var props = Actor.FromFunc(c =>
            {
                switch (c.Message)
                {
                    case int i:
                        if (i == n)
                        {
                            recvSw.Stop();
                            Console.WriteLine($"recv {(int) (n / recvSw.Elapsed.TotalSeconds / 1000)}K/sec ({name})");
                        }
                        break;
                }
                return Actor.Done;
            })
            .WithMailbox(mailbox);
        var pid = Actor.Spawn(props);
        sendSw.Start();
        recvSw.Start();
        for (var i = 1; i <= n; i++)
        {
            pid.Tell(i);
        }
        sendSw.Stop();
        Console.WriteLine($"send {(int) (n / sendSw.Elapsed.TotalSeconds / 1000)}K/sec ({name})");
    }
}