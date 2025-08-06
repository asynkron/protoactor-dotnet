// -----------------------------------------------------------------------
// <copyright file="StashingTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests;

// actor that stashes first string message and processes it when Receive is called
public class StashingActor : IActor
{
    private readonly List<string> _processed;
    private CapturedContext? _captured;
    private bool _stashed;

    public StashingActor(List<string> processed)
    {
        _processed = processed;
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            string msg when msg == "unstash" => HandleUnstash(),
            string msg                        => HandleString(msg, context),
            _                                 => Task.CompletedTask
        };

        Task HandleUnstash()
        {
            return _captured?.Receive() ?? Task.CompletedTask;
        }

        Task HandleString(string msg, IContext ctx)
        {
            if (!_stashed)
            {
                _captured = ctx.Capture();
                _stashed = true;
                return Task.CompletedTask; // do not process yet
            }

            _processed.Add(msg); // processed on replay
            return Task.CompletedTask;
        }
    }
}

// actor used to test Apply()
public class ApplyActor : IActor
{
    private readonly List<string> _observed;
    private CapturedContext? _captured;

    public bool ContextCorrupted { get; private set; }

    public ApplyActor(List<string> observed) => _observed = observed;

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            string msg when msg == "stash" => HandleStash(context),
            string msg when msg == "apply" => HandleApply(context),
            _                                => Task.CompletedTask
        };

        Task HandleStash(IContext ctx)
        {
            _captured = ctx.Capture();
            return Task.CompletedTask;
        }

        Task HandleApply(IContext ctx)
        {
            var current = ctx.Capture(); // capture current context

            _captured?.Apply(); // restore stashed envelope
            _observed.Add(ctx.Message?.ToString() ?? string.Empty); // record restored message

            current.Apply(); // restore current message
            if (!Equals(ctx.Message, current.MessageEnvelope.Message))
            {
                ContextCorrupted = true;
            }

            _observed.Add(ctx.Message?.ToString() ?? string.Empty); // record current message after restore

            return Task.CompletedTask;
        }
    }
}

public class StashingTests
{
    [Fact]
    public async Task Replayed_message_is_processed_only_once()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var processed = new List<string>();

        var props = Props.FromProducer(() => new StashingActor(processed));
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hello"); // stashed
        processed.Should().BeEmpty();

        system.Root.Send(pid, "unstash");
        await system.Root.PoisonAsync(pid);

        processed.Should().BeEquivalentTo(new[] { "hello" });
    }

    [Fact]
    public async Task Apply_restores_message_without_corrupting_context()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var processed = new List<string>();
        ApplyActor? actor = null;

        var props = Props.FromProducer(() => actor = new ApplyActor(processed));
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "stash");
        system.Root.Send(pid, "apply");

        await system.Root.PoisonAsync(pid);

        processed.Should().BeEquivalentTo(new[] { "stash", "apply" });
        actor!.ContextCorrupted.Should().BeFalse();
    }
}

