// -----------------------------------------------------------------------
// <copyright file="BaseFutureTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Xunit;

namespace Proto.Tests
{
    public abstract class BaseFutureTests
    {
        protected const int BatchSize = 1000;
        protected static readonly ActorSystem System = new();
        protected static readonly RootContext Context = System.Root;

        protected abstract IFuture GetFuture();

        [Fact]
        public async Task Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond("hey");
                        return Task.CompletedTask;
                    }
                )
            );

            var future = GetFuture();

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

            var future = GetFuture();

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

            var futures = new IFuture[BatchSize];

            for (int i = 0; i < BatchSize; i++)
            {
                var future = GetFuture();
                Context.Request(pid, i, future.Pid);
                futures[i] = future;
            }

            var replies = await Task.WhenAll(futures.Select(future => future.Task));

            replies.Should().BeInAscendingOrder().And.HaveCount(BatchSize);
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
            var futures = new IFuture[batchSize];

            for (int i = 0; i < batchSize; i++)
            {
                var future = GetFuture();
                futures[i] = future;
                Context.Request(pid, i, future.Pid);
            }

            futures.Invoking(async f => { await Task.WhenAll(f.Select(future => future.GetTask(CancellationTokens.WithTimeout(50)))); }
            ).Should().Throw<TimeoutException>();
        }
    }
}