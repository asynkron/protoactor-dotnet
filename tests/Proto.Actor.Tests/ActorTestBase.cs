// -----------------------------------------------------------------------
// <copyright file="ActorTestBase.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public abstract class ActorTestBase : IAsyncLifetime
    {
        protected readonly ActorSystem System;
        protected readonly RootContext Context;

        protected PID SpawnForwarderFromFunc(Receive forwarder) => Context.Spawn(Props.FromFunc(forwarder));

        protected PID SpawnActorFromFunc(Receive receive) => Context.Spawn(Props.FromFunc(receive));

        
        protected ActorTestBase()
        {
            System = new();
            Context = System.Root;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await System.ShutdownAsync();
        }
    }
}