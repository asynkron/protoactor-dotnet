using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class BatchFutureTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        private readonly ITestOutputHelper output;

        public BatchFutureTests(ITestOutputHelper output) => this.output = output;

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
            var future = batch.Futures.Take(1).Single();
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
            var future = batch.Futures.Take(1).Single();
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

            using var batch = new FutureBatchProcess(System, 1000, CancellationTokens.WithTimeout(1000));
            var futures = batch.Futures.Take(1000).ToArray();

            for (int i = 0; i < futures.Length; i++)
            {
                Context.Request(pid, i, futures[i].Pid);
            }

            var replies = await Task.WhenAll(futures.Select(future => future.Task));

            replies.Should().BeInAscendingOrder().And.HaveCount(1000);
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

            using var batch = new FutureBatchProcess(System, 1000, CancellationTokens.WithTimeout(50));
            var futures = batch.Futures.Take(1000).ToArray();

            for (int i = 0; i < futures.Length; i++)
            {
                Context.Request(pid, i, futures[i].Pid);
            }

            futures.Invoking(async f => {
                    await Task.WhenAll(f.Select(future => future.Task));
                }
            ).Should().Throw<TimeoutException>();
        }
    }
}