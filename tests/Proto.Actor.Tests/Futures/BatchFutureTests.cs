using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Xunit;

namespace Proto.Tests
{
    public class BatchFutureTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        [Fact]
        public async Task Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond("hey");
                        return Task.CompletedTask;
                    }
                )
            );

            using var batch = new FutureBatchProcess(System, 1, CancellationTokens.WithTimeout(1000));
            var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");


            Context.Request(pid, "hello", future.Pid);

            var reply = await future.Task;
            reply.Should().Be("hey");
        }

        [Fact]
        public async Task Given_Actor_When_ReplyIsNull_Should_Return()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond(null!);
                        return Task.CompletedTask;
                    }
                )
            );

            using var batch = new FutureBatchProcess(System, 1, CancellationTokens.WithTimeout(1000));
            var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");

            Context.Request(pid, "hello", future.Pid);

            var reply = await future.Task;
            reply.Should().BeNull();
        }

        [Fact]
        public async Task Futures_should_map_to_correct_response()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                        return Task.CompletedTask;
                    }
                )
            );

            var batchSize = 1000;
            using var batch = new FutureBatchProcess(System, batchSize, CancellationTokens.WithTimeout(batchSize));
            var futures = new IFuture[batchSize];

            for (int i = 0; i < batchSize; i++)
            {
                var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");
                Context.Request(pid, i, future.Pid);
                futures[i] = future;
            }

            var replies = await Task.WhenAll(futures.Select(future => future.Task));

            replies.Should().BeInAscendingOrder().And.HaveCount(batchSize);
        }

        [Fact]
        public void Timeouts_should_give_timeout_exception()
        {
            var pid = Context.Spawn(Props.FromFunc(async ctx => {
                        if (ctx.Sender is not null)
                        {
                            await Task.Delay(1);
                            ctx.Respond(ctx.Message!);
                        }
                    }
                )
            );

            var batchSize = 1000;
            using var batch = new FutureBatchProcess(System, batchSize, CancellationTokens.WithTimeout(50));
            var futures = new IFuture[batchSize];

            for (int i = 0; i < batchSize; i++)
            {
                var future = batch.TryGetFuture() ?? throw new Exception("Not able to get future");
                futures[i] = future;
                Context.Request(pid, i, future.Pid);
            }

            futures.Invoking(async f => { await Task.WhenAll(f.Select(future => future.Task)); }
            ).Should().Throw<TimeoutException>();
        }

        [Fact]
        public async Task Batch_contexts_handles_batch_correctly()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                        return Task.CompletedTask;
                    }
                )
            );
            const int size = 1000;

            var cancellationToken = CancellationTokens.WithTimeout(1000);
            using var batchContext = System.Root.CreateBatchContext(size, cancellationToken);

            var tasks = new Task<object>[size];

            for (int i = 0; i < size; i++)
            {
                tasks[i] = batchContext.RequestAsync<object>(pid, i, cancellationToken);
            }

            var replies = await Task.WhenAll(tasks);

            replies.Should().BeInAscendingOrder().And.HaveCount(1000);
        }
    }
}