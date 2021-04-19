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
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;
        
        [Fact]
        public void CanListPidsInProcessRegistry()
        {
            var props = Props.FromProducer(() => new MyDiagnosticsActor());

            var pids = System.ProcessRegistry.SearchByName("MyActor");
            Assert.Empty(pids);
            
            var pid = Context.SpawnNamed(props,"MyActor");
            
            pids = System.ProcessRegistry.SearchByName("MyActor");
            Assert.Single(pids);
        }
        
        [Fact]
        public async Task CanGetDiagnosticsStringFromActorDiagnostics()
        {
            var props = Props.FromProducer(() => new MyDiagnosticsActor());

            var pid = Context.Spawn(props);

            var res = await DiagnosticTools.GetDiagnosticsString(System, pid);
            Assert.Contains("Hello World", res, StringComparison.InvariantCulture);
        }
    }
}