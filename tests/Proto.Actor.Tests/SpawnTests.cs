// -----------------------------------------------------------------------
//  <copyright file="SpawnTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests;

public class SpawnTests
{
    [Fact]
    public async Task Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
    {
        var system = new ActorSystem();
        await using var __ = system;
        var context = system.Root;

        var spawnedPid = PID.FromAddress("test", "test");

        var props = Props.FromFunc(EmptyReceive)
            .WithSpawner((s, id, p, parent, _) => spawnedPid);

        var pid = context.Spawn(props);

        Assert.Same(spawnedPid, pid);
    }

    [Fact]
    public async Task Given_Existing_Name_SpawnNamedShouldThrow()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var props = Props.FromFunc(EmptyReceive);

        var uniqueName = Guid.NewGuid().ToString();
        context.SpawnNamed(props, uniqueName);
        var x = Assert.Throws<ProcessNameExistException>(() => { context.SpawnNamed(props, uniqueName); });
        Assert.Equal(uniqueName, x.Name);
    }

    [Fact]
    public async Task Given_Existing_Name_SpawnPrefixShouldReturnPID()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var props = Props.FromFunc(EmptyReceive);

        context.SpawnNamed(props, "existing");
        var pid = context.SpawnPrefix(props, "existing");
        Assert.NotNull(pid);
    }
}