// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class SpawnTests
    {
        [Fact]
        public void Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            var spawnedPid = new PID("test", "test");
            var props = Actor.FromFunc(EmptyReceive)
                .WithSpawner((id, p, parent) => spawnedPid);

            var pid = Actor.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }
        
        [Fact]
        public void Given_Existing_Name_SpawnNamedShouldThrow()
        {
            var props = Actor.FromFunc(EmptyReceive);

            var uniqueName = Guid.NewGuid().ToString();
            Actor.SpawnNamed(props,uniqueName);
            var x = Assert.Throws<ProcessNameExistException>(() =>
            {
                Actor.SpawnNamed(props, uniqueName);
            });
            Assert.Equal(uniqueName,x.Name);
        }
        
        [Fact]
        public void Given_Existing_Name_SpawnPrefixShouldReturnPID()
        {
            var props = Actor.FromFunc(EmptyReceive);

            Actor.SpawnNamed(props,"existing");
            var pid = Actor.SpawnPrefix(props, "existing");
            Assert.NotNull(pid);
        }
    }
}