// -----------------------------------------------------------------------
// <copyright file="PoisonTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests
{
    public class PoisonTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        private static readonly Props EchoProps = Props.FromFunc(ctx => {
                if (ctx.Sender != null) ctx.Respond(ctx.Message!);

                return Task.CompletedTask;
            }
        );

        [Fact]
        public async Task PoisonReturnsIfPidDoesNotExist()
        {
            var deadPid = PID.FromAddress(System.Address, "nowhere");
            var timeout = Task.Delay(1000);

            var poisonTask = Context.PoisonAsync(deadPid);

            await Task.WhenAny(timeout, poisonTask);

            poisonTask.IsCompleted.Should().BeTrue("Or we did not get a response when poisoning a missing pid");
        }

        [Fact]
        public async Task PoisonTerminatesActor()
        {
            var pid = Context.Spawn(EchoProps);

            const string message = "hello";
            (await Context.RequestAsync<string>(pid, message)).Should().Be(message);

            var timeout = Task.Delay(1000);
            var poisonTask = Context.PoisonAsync(pid);
            await Task.WhenAny(timeout, poisonTask);

            poisonTask.IsCompleted.Should().BeTrue("Or we did not get a response when poisoning a live pid");

            Context.Invoking(ctx => ctx.RequestAsync<string>(pid, message)).Should().ThrowExactly<DeadLetterException>();
        }
    }
}