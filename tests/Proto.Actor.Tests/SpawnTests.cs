﻿// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class SpawnTests : ActorTestBase
    {
        [Fact]
        public void Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            var spawnedPid = PID.FromAddress("test", "test");
            var props = Props.FromFunc(EmptyReceive)
                .WithSpawner((s, id, p, parent) => spawnedPid);

            var pid = Context.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }

        [Fact]
        public void Given_Existing_Name_SpawnNamedShouldThrow()
        {
            var props = Props.FromFunc(EmptyReceive);

            var uniqueName = Guid.NewGuid().ToString();
            Context.SpawnNamed(props, uniqueName);
            var x = Assert.Throws<ProcessNameExistException>(() => { Context.SpawnNamed(props, uniqueName); });
            Assert.Equal(uniqueName, x.Name);
        }

        [Fact]
        public void Given_Existing_Name_SpawnPrefixShouldReturnPID()
        {
            var props = Props.FromFunc(EmptyReceive);

            Context.SpawnNamed(props, "existing");
            var pid = Context.SpawnPrefix(props, "existing");
            Assert.NotNull(pid);
        }
    }
}