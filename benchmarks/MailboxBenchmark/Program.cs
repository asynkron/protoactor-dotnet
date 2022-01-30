// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using BenchmarkDotNet.Running;

class Program
{
    private static void Main() => BenchmarkRunner.Run<MailboxBenchmark>();
}