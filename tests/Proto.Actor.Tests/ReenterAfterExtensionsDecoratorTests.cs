using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests;

public class ReenterAfterExtensionsDecoratorTests : ActorTestBase
{
    private sealed class CountingReenterDecorator : ActorContextDecorator
    {
        public int TaskCalls { get; private set; }
        public int GenericTaskCalls { get; private set; }

        public CountingReenterDecorator(IContext context) : base(context) { }

        public override void ReenterAfter(Task target, Func<Task, Task> action)
        {
            TaskCalls++;
            base.ReenterAfter(target, action);
        }

        public override void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            GenericTaskCalls++;
            base.ReenterAfter(target, action);
        }
    }

    private async Task Verify(Func<IContext, TaskCompletionSource<bool>, Task> invoke,
        int expectedTaskCalls, int expectedGenericTaskCalls)
    {
        CountingReenterDecorator? decorator = null;

        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is TaskCompletionSource<bool> tcs)
            {
                invoke(ctx, tcs);
            }

            return Task.CompletedTask;
        }).WithContextDecorator(c => decorator = new CountingReenterDecorator(c));

        var pid = Context.Spawn(props);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Context.Send(pid, tcs);
        await tcs.Task.ConfigureAwait(false);

        decorator!.TaskCalls.Should().Be(expectedTaskCalls);
        decorator.GenericTaskCalls.Should().Be(expectedGenericTaskCalls);
    }

    [Fact]
    public Task Task_Action_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.CompletedTask, () => tcs.SetResult(true));
            return Task.CompletedTask;
        }, 1, 0);

    [Fact]
    public Task Task_Func_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.CompletedTask, async () =>
            {
                tcs.SetResult(true);
                await Task.CompletedTask;
            });
            return Task.CompletedTask;
        }, 1, 0);

    [Fact]
    public Task Task_ActionTask_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            var task = Task.CompletedTask;
            ctx.ReenterAfter(task, completed => tcs.SetResult(completed.IsCompleted));
            return Task.CompletedTask;
        }, 1, 0);

    [Fact]
    public Task Generic_Action_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.FromResult(1), () => tcs.SetResult(true));
            return Task.CompletedTask;
        }, 0, 1);

    [Fact]
    public Task Generic_Func_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.FromResult(1), async () =>
            {
                tcs.SetResult(true);
                await Task.CompletedTask;
            });
            return Task.CompletedTask;
        }, 0, 1);

    [Fact]
    public Task Generic_ActionTask_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.FromResult(1), task => tcs.SetResult(task.IsCompleted));
            return Task.CompletedTask;
        }, 0, 1);

    [Fact]
    public Task Generic_ActionResult_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.FromResult(1), result => tcs.SetResult(result == 1));
            return Task.CompletedTask;
        }, 0, 1);

    [Fact]
    public Task Generic_FuncResult_Should_Call_Decorator() =>
        Verify((ctx, tcs) =>
        {
            ctx.ReenterAfter(Task.FromResult(1), async result =>
            {
                tcs.SetResult(result == 1);
                await Task.CompletedTask;
            });
            return Task.CompletedTask;
        }, 0, 1);
}
