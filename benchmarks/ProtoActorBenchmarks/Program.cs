// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using BenchmarkDotNet.Running;

namespace ProtoActorBenchmarks
{
    public static class Program
    {
        public static void Main() => BenchmarkRunner.Run<SkyNetBenchmark>();
    }
}