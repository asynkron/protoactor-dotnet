// -----------------------------------------------------------------------
// <copyright file="CaptureContextTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests
{
    public record Unstash;

    public record UnstashResponse(object Unstash);

    public record UnstashResult(object Message, PID Sender);

    public class CaptureContextActor : IActor
    {
        private readonly Behavior _behavior;
        private readonly Queue<UnstashResult> _results;
        private readonly List<CapturedContext> _stash = new();

        public CaptureContextActor(Queue<UnstashResult> results)
        {
            _behavior = new Behavior(CapturingBehavior);
            _results = results;
        }

        public Task ReceiveAsync(IContext context) => _behavior.ReceiveAsync(context);

        //this is the initial behavior
        //capture everything, except for Unstash message
        public async Task CapturingBehavior(IContext context)
        {
            if (context.Message is Unstash unStash)
            {
                await ProcessStash(context, unStash);
                return;
            }

            if (context.Message is not InfrastructureMessage)
            {
                // if the message is a user message, stash it
                _stash.Add(context.Capture());
            }
        }

        private async Task ProcessStash(IContext context, Unstash message)
        {
            _behavior.Become(RunningBehavior);

            foreach (var c in _stash)
            {
                await c.Receive();
            }

            context.Respond(new UnstashResponse(context.Message!));
        }

        //this behavior is called when messages are being unstashed, and afterwards
        public Task RunningBehavior(IContext context)
        {
            _results.Enqueue(new UnstashResult(context.Message!, context.Sender!));
            return Task.CompletedTask;
        }
    }

    public class CaptureContextTests
    {
        [Fact]
        public async Task can_receive_captured_context()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var results = new Queue<UnstashResult>();

            var props = Props.FromProducer(() => new CaptureContextActor(results));
            var pid = context.Spawn(props);

            for (var i = 0; i < 10; i++)
            {
                context.Request(pid, $"message{i}", PID.FromAddress("someaddress", $"somesender{i}"));
            }

            context.Send(pid, new Unstash());
            await context.PoisonAsync(pid);

            for (var i = 0; i < 10; i++)
            {
                var next = results.Dequeue();
                next.Message.Should().Be($"message{i}");
                next.Sender.Id.Should().Be($"somesender{i}");
            }
        }

        [Fact]
        public async Task can_continue_after_processing_capture()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var results = new Queue<UnstashResult>();

            var props = Props.FromProducer(() => new CaptureContextActor(results));
            var pid = context.Spawn(props);

            for (var i = 0; i < 10; i++)
            {
                context.Request(pid, $"message{i}", PID.FromAddress("someaddress", $"somesender{i}"));
            }

            var unstash = new Unstash();
            var response = await context.RequestAsync<UnstashResponse>(pid, unstash, TimeSpan.FromSeconds(1));
            response.Should().NotBeNull();
            Assert.Same(response.Unstash, unstash);
            await context.PoisonAsync(pid);

            for (var i = 0; i < 10; i++)
            {
                var next = results.Dequeue();
                next.Message.Should().Be($"message{i}");
                next.Sender.Id.Should().Be($"somesender{i}");
            }
        }
    }
}