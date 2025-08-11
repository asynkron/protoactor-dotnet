// -----------------------------------------------------------------------
// <copyright file="SharedFutureTokenReuseTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests;

public class SharedFutureTokenReuseTests : ActorTestBase
{
    [Fact]
    public async Task Should_not_reuse_cancelled_tokens_in_shared_futures()
    {
        // actor that simply echoes back the incoming message
        var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Sender is not null)
                {
                    ctx.Respond(ctx.Message!);
                }

                return Task.CompletedTask;
            }));

        const int iterations = 2_000_000;

        var options = new ParallelOptions
        {
            // allow many concurrent futures to overlap
            MaxDegreeOfParallelism = Environment.ProcessorCount * 4
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, iterations), options, async (i, _) =>
        {
            using var future = System.Future.Get();

            if ((i & 1) == 0)
            {
                // successful path, request should complete before timeout
                Context.Request(pid, i, future.Pid);
                var token = CancellationTokens.FromSeconds(5);
                token.IsCancellationRequested.Should().BeFalse();
                var res = await future.GetTask(token);
                res.Should().Be(i);
            }
            else
            {
                // timeout path, no message is sent and token is cancelled immediately
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                await Assert.ThrowsAsync<TimeoutException>(() => future.GetTask(cts.Token));
            }
        });
    }
}

