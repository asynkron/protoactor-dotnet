﻿// -----------------------------------------------------------------------
// <copyright file="PoisonTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Utils;
using Xunit;

namespace Proto.Tests
{
    public class PoisonTests
    {
        private static readonly Props EchoProps = Props.FromFunc(ctx => {
                if (ctx.Sender != null) ctx.Respond(ctx.Message!);

                return Task.CompletedTask;
            }
        );

        [Fact]
        public async Task PoisonReturnsIfPidDoesNotExist()
        {
            await using var system = new ActorSystem();
            var deadPid = PID.FromAddress(system.Address, "nowhere");

            var poisonTask = system.Root.PoisonAsync(deadPid);

            var completed = await poisonTask.WaitUpTo(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            completed.Should().BeTrue("Or we did not get a response when poisoning a missing pid");
        }

        [Fact]
        public async Task PoisonTerminatesActor()
        {
            await using var system = new ActorSystem();

            var pid = system.Root.Spawn(EchoProps);

            const string message = "hello";
            (await system.Root.RequestAsync<string>(pid, message).ConfigureAwait(false)).Should().Be(message);

            var poisonTask = system.Root.PoisonAsync(pid);
            var completed = await poisonTask.WaitUpTo(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            completed.Should().BeTrue("Or we did not get a response when poisoning a live pid");

            system.Root.Invoking(ctx => ctx.RequestAsync<string>(pid, message)).Should().ThrowExactly<DeadLetterException>();
        }
    }
}