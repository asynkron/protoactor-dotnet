// -----------------------------------------------------------------------
// <copyright file="DiagnosticsTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Diagnostics;
using Xunit;

namespace Proto.Tests.Diagnostics;

public class MyDiagnosticsActor : IActor, IActorDiagnostics
{
    public Task ReceiveAsync(IContext context) => Task.CompletedTask;

    public string GetDiagnosticsString() => "Hello World";
}

public class DiagnosticsTests
{
    [Fact]
    public async Task CanListPidsInProcessRegistry()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var props = Props.FromProducer(() => new MyDiagnosticsActor());

        var pids = system.ProcessRegistry.Find("MyActor");
        Assert.Empty(pids);

        context.SpawnNamed(props, "MyActor");

        pids = system.ProcessRegistry.Find("MyActor");
        Assert.Single(pids);
    }

    [Fact]
    public async Task CanGetDiagnosticsStringFromActorDiagnostics()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var props = Props.FromProducer(() => new MyDiagnosticsActor());

        var pid = context.Spawn(props);

        var res = await DiagnosticTools.GetDiagnosticsString(system, pid);
        Assert.Contains("Hello World", res, StringComparison.InvariantCulture);
    }
}