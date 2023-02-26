// -----------------------------------------------------------------------
// <copyright file="PoisonTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Utils;
using Xunit;

namespace Proto.Tests;

public class PoisonTests
{
    private static readonly Props EchoProps = Props.FromFunc(ctx =>
        {
            if (ctx.Sender != null)
            {
                ctx.Respond(ctx.Message!);
            }

            return Task.CompletedTask;
        }
    );

    [Fact]
    public async Task PoisonReturnsIfPidDoesNotExist()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var deadPid = PID.FromAddress(system.Address, "nowhere");

        var poisonTask = system.Root.PoisonAsync(deadPid);

        var completed = await poisonTask.WaitUpTo(TimeSpan.FromSeconds(10));

        completed.Should().BeTrue("Or we did not get a response when poisoning a missing pid");
    }

    [Fact]
    public async Task PoisonTerminatesActor()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var pid = system.Root.Spawn(EchoProps);

        const string message = "hello";
        (await system.Root.RequestAsync<string>(pid, message)).Should().Be(message);

        var poisonTask = system.Root.PoisonAsync(pid);
        var completed = await poisonTask.WaitUpTo(TimeSpan.FromSeconds(10));

        completed.Should().BeTrue("Or we did not get a response when poisoning a live pid");

        await system.Root.Invoking(ctx => ctx.RequestAsync<string>(pid, message))
            .Should()
            .ThrowExactlyAsync<DeadLetterException>();
    }
}