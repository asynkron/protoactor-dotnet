// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using BenchmarkDotNet.Running;
using Proto.Mailbox;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<MailboxBenchmark>();
    }
}