// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

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
    }
}