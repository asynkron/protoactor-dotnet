// -----------------------------------------------------------------------
// <copyright file="SharedFutureTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Xunit;

namespace Proto.Tests;

public class SharedFutureTests : BaseFutureTests
{
    private readonly SharedFutureProcess _sharedFutureProcess;

    public SharedFutureTests()
    {
        _sharedFutureProcess = new SharedFutureProcess(System, BatchSize);
    }

    protected override IFuture GetFuture() =>
        _sharedFutureProcess.TryCreateHandle() ?? throw new Exception("No futures available");

    [Fact]
    public async Task Should_reuse_completed_futures()
    {
        // first test should use all available futures, and should return them when done
        await Futures_should_map_to_correct_response();

        //After they are returned, they should be available for re-use.
        await Futures_should_map_to_correct_response();
        await Futures_should_map_to_correct_response();
        await Futures_should_map_to_correct_response();
    }

    [Fact]
    public void Should_not_give_out_more_futures_than_size_allows()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            GetFuture().Should().NotBeNull();
        }

        this.Invoking(it => it.GetFuture()).Should().Throw<Exception>();
    }

    [Fact]
    public async Task Should_wrap_request_ids_without_hitting_zero()
    {
        const int slotCount = 4;
        const int forcedMaxRequestId = slotCount * 2;

        using var process = new SharedFutureProcess(System, slotCount);

        // Force a tiny wrap-around window so we can hit it quickly in a unit test.
        typeof(SharedFutureProcess)
            .GetField("_maxRequestId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(process, forcedMaxRequestId);

        var reserved = new List<IFuture>();
        IFuture? future = null;

        while (future is null)
        {
            var candidate = process.TryCreateHandle();
            candidate.Should().NotBeNull();

            if (candidate!.Pid.RequestId == slotCount)
            {
                future = candidate;
                break;
            }

            reserved.Add(candidate);
        }

        var echo = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Sender is not null)
                    {
                        ctx.Respond(ctx.Message!);
                    }

                    return Task.CompletedTask;
                }
            )
        );

        var observedRequestIds = new List<uint>();

        for (var iteration = 0; iteration < 3; iteration++)
        {
            future!.Pid.RequestId.Should().NotBe(0u);
            var currentRequestId = future.Pid.RequestId;
            observedRequestIds.Add(currentRequestId);

            Context.Request(echo, iteration, future.Pid);

            var reply = await future.Task;
            reply.Should().Be(iteration);

            if (iteration < 2)
            {
                // SharedFutureProcess increments by the slot count and wraps into [1, max] using the same arithmetic as the runtime.
                var expectedNext = (uint)(((currentRequestId - 1u + (uint)slotCount) % (uint)forcedMaxRequestId) + 1u);

                future = process.TryCreateHandle() ?? throw new Exception("Expected shared future handle");
                future.Pid.RequestId.Should().Be(expectedNext);
            }
        }

        observedRequestIds.Should().OnlyContain(id => id > 0u && id <= (uint)forcedMaxRequestId);

        foreach (var handle in reserved)
        {
            handle.Dispose();
        }

        Context.Stop(echo);
    }
}
