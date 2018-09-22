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
        private static readonly RootContext Context = new RootContext();
        [Fact]
        public void Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            var spawnedPid = new PID("test", "test");
            var props = Props.FromFunc(EmptyReceive)
                .WithSpawner((id, p, parent) => spawnedPid);

            var pid = Context.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }
        
        [Fact]
        public void Given_Existing_Name_SpawnNamedShouldThrow()
        {
            var props = Props.FromFunc(EmptyReceive);

            var uniqueName = Guid.NewGuid().ToString();
            Context.SpawnNamed(props,uniqueName);
            var x = Assert.Throws<ProcessNameExistException>(() =>
            {
                Context.SpawnNamed(props, uniqueName);
            });
            Assert.Equal(uniqueName,x.Name);
        }
        
        [Fact]
        public void Given_Existing_Name_SpawnPrefixShouldReturnPID()
        {
            var props = Props.FromFunc(EmptyReceive);

            Context.SpawnNamed(props,"existing");
            var pid = Context.SpawnPrefix(props, "existing");
            Assert.NotNull(pid);
        }
    }
}