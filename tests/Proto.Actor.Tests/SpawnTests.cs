// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class SpawnTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        [Fact]
        public void Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            PID spawnedPid = PID.FromAddress("test", "test");
            Props props = Props.FromFunc(EmptyReceive)
                .WithSpawner((s, id, p, parent) => spawnedPid);

            PID pid = Context.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }

        [Fact]
        public void Given_Existing_Name_SpawnNamedShouldThrow()
        {
            Props props = Props.FromFunc(EmptyReceive);

            string uniqueName = Guid.NewGuid().ToString();
            Context.SpawnNamed(props, uniqueName);
            ProcessNameExistException x = Assert.Throws<ProcessNameExistException>(() =>
            {
                Context.SpawnNamed(props, uniqueName);
            });
            Assert.Equal(uniqueName, x.Name);
        }

        [Fact]
        public void Given_Existing_Name_SpawnPrefixShouldReturnPID()
        {
            Props props = Props.FromFunc(EmptyReceive);

            Context.SpawnNamed(props, "existing");
            PID pid = Context.SpawnPrefix(props, "existing");
            Assert.NotNull(pid);
        }
    }
}
