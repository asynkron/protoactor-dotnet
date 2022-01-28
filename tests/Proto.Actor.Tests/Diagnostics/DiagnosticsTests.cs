// -----------------------------------------------------------------------
// <copyright file="DiagnosticsTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto.Diagnostics;
using Xunit;

namespace Proto.Tests.Diagnostics
{
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
            await using var System = new ActorSystem();
            var Context = System.Root;

            var props = Props.FromProducer(() => new MyDiagnosticsActor());

            var pids = System.ProcessRegistry.SearchByName("MyActor");
            Assert.Empty(pids);
            
            Context.SpawnNamed(props,"MyActor");
            
            pids = System.ProcessRegistry.SearchByName("MyActor");
            Assert.Single(pids);
        }
        
        [Fact]
        public async Task CanGetDiagnosticsStringFromActorDiagnostics()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var props = Props.FromProducer(() => new MyDiagnosticsActor());

            var pid = Context.Spawn(props);

            var res = await DiagnosticTools.GetDiagnosticsString(System, pid);
            Assert.Contains("Hello World", res, StringComparison.InvariantCulture);
        }
    }
}