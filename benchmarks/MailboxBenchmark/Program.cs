// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using BenchmarkDotNet.Running;

class Program
{
    static void Main() => BenchmarkRunner.Run<MailboxBenchmark>();
}