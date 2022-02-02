// -----------------------------------------------------------------------
// <copyright file="ActorTestBase.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public abstract class ActorTestBase : IAsyncLifetime
    {
        protected readonly RootContext Context;
        protected readonly ActorSystem System;

        protected ActorTestBase()
        {
            System = new ActorSystem();
            Context = System.Root;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync() => await System.ShutdownAsync();

        protected PID SpawnForwarderFromFunc(Receive forwarder) => Context.Spawn(Props.FromFunc(forwarder));

        protected PID SpawnActorFromFunc(Receive receive) => Context.Spawn(Props.FromFunc(receive));
    }
}