// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class SpawnTests
    {
        [Fact]
        public async Task Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var spawnedPid = PID.FromAddress("test", "test");
            var props = Props.FromFunc(EmptyReceive)
                .WithSpawner((s, id, p, parent) => spawnedPid);

            var pid = Context.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }

        [Fact]
        public async Task Given_Existing_Name_SpawnNamedShouldThrow()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var props = Props.FromFunc(EmptyReceive);

            var uniqueName = Guid.NewGuid().ToString();
            Context.SpawnNamed(props, uniqueName);
            var x = Assert.Throws<ProcessNameExistException>(() => { Context.SpawnNamed(props, uniqueName); });
            Assert.Equal(uniqueName, x.Name);
        }

        [Fact]
        public async Task Given_Existing_Name_SpawnPrefixShouldReturnPID()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var props = Props.FromFunc(EmptyReceive);

            Context.SpawnNamed(props, "existing");
            var pid = Context.SpawnPrefix(props, "existing");
            Assert.NotNull(pid);
        }
    }
}