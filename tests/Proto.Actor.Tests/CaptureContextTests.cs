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
    public record Unstash();

    public record UnstashResult(object Message, PID Sender);
    
    public class CaptureContextActor : IActor
    {
        private readonly List<CapturedContext> _stash = new();
        private readonly Behavior _behavior;
        private readonly Queue<UnstashResult> _results;

        public Task ReceiveAsync(IContext context) => _behavior.ReceiveAsync(context);
        public CaptureContextActor(Queue<UnstashResult> results)
        {
            _behavior = new Behavior(CapturingBehavior);
            _results = results;
        }

        //this is the initial behavior
        //capture everything, except for Unstash message
        public async Task CapturingBehavior(IContext context)
        {
            if (context.Message is Unstash)
            {
                await ProcessStash();
                return;
            }

            if (context.Message is not InfrastructureMessage)
            {
                // if the message is a user message, stash it
                _stash.Add(context.Capture());
            }
        }

        private async Task ProcessStash()
        {
            _behavior.Become(RunningBehavior);
            foreach (var c in _stash)
            {
                await c.Receive();
            }
        }
        

        //this behavior is called when messages are being unstashed, and afterwards
        public async Task RunningBehavior(IContext context)
        {
            _results.Enqueue(new UnstashResult(context.Message!, context.Sender!));
        }


        
    }
    public class CaptureContextTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;
        
        [Fact]
        public async Task can_receive_captured_context()
        {
            var results = new Queue<UnstashResult>();

            var props = Props.FromProducer(() => new CaptureContextActor(results));
            var pid = Context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                Context.Request(pid, $"message{i}", PID.FromAddress("someaddress", $"somesender{i}"));
            }
            Context.Send(pid, new Unstash());
            await Context.PoisonAsync(pid);
            
            for (int i = 0; i < 10; i++)
            {
                var next = results.Dequeue();
                next.Message.Should().Be($"message{i}");
                next.Sender.Id.Should().Be($"somesender{i}");
            }
        }
    }
}