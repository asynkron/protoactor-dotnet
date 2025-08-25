using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));

BenchmarkRunner.Run<ThrottleBenchmarks.ThrottleBenchmarks>(config);
