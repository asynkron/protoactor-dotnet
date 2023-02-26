using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Xunit;

namespace Proto.Tests;

public class BatchFutureTests : ActorTestBase
{
    [Fact]
    public async Task Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
    {
        var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string)
                    {
                        ctx.Respond("hey");
                    }

                    return Task.CompletedTask;
                }
            )
        );

        using var batch = new FutureBatchProcess(System, 1, CancellationTokens.FromSeconds(5));
        var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");

        Context.Request(pid, "hello", future.Pid);

        var reply = await future.Task;
        reply.Should().Be("hey");
    }

    [Fact]
    public async Task Given_Actor_When_ReplyIsNull_Should_Return()
    {
        var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string)
                    {
                        ctx.Respond(null!);
                    }

                    return Task.CompletedTask;
                }
            )
        );

        using var batch = new FutureBatchProcess(System, 1, CancellationTokens.FromSeconds(5));
        var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");

        Context.Request(pid, "hello", future.Pid);

        var reply = await future.Task;
        reply.Should().BeNull();
    }

    [Fact]
    public async Task Futures_should_map_to_correct_response()
    {
        var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Sender is not null)
                    {
                        ctx.Respond(ctx.Message!);
                    }

                    return Task.CompletedTask;
                }
            )
        );

        var batchSize = 100;
        using var batch = new FutureBatchProcess(System, batchSize, CancellationTokens.FromSeconds(5));
        var futures = new IFuture[batchSize];

        for (var i = 0; i < batchSize; i++)
        {
            var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");
            Context.Request(pid, i, future.Pid);
            futures[i] = future;
        }

        var replies = await Task.WhenAll(futures.Select(future => future.Task));

        replies.Should().BeInAscendingOrder().And.HaveCount(batchSize);
    }

    [Fact]
    public async Task Timeouts_should_give_timeout_exception()
    {
        var pid = Context.Spawn(Props.FromFunc(async ctx =>
                {
                    if (ctx.Sender is not null)
                    {
                        await Task.Delay(1);
                        ctx.Respond(ctx.Message!);
                    }
                }
            )
        );

        var batchSize = 1000;
        using var cts = new CancellationTokenSource(50);
        using var batch = new FutureBatchProcess(System, batchSize, cts.Token);
        var futures = new IFuture[batchSize];

        for (var i = 0; i < batchSize; i++)
        {
            var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");
            futures[i] = future;
            Context.Request(pid, i, future.Pid);
        }

        await futures.Invoking(async f => { await Task.WhenAll(f.Select(future => future.Task)); }
            )
            .Should()
            .ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Batch_contexts_handles_batch_correctly()
    {
        var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Sender is not null)
                    {
                        ctx.Respond(ctx.Message!);
                    }

                    return Task.CompletedTask;
                }
            )
        );

        const int size = 100;

        var cancellationToken = CancellationTokens.FromSeconds(5);
        using var batchContext = System.Root.CreateBatchContext(size, cancellationToken);

        var tasks = new Task<object>[size];

        for (var i = 0; i < size; i++)
        {
            tasks[i] = batchContext.RequestAsync<object>(pid, i, cancellationToken);
        }

        var replies = await Task.WhenAll(tasks);

        replies.Should().BeInAscendingOrder().And.HaveCount(size);
    }
}