using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class LocalContextTests
    {
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));

        [Fact]
        public void Given_Context_ctor_should_set_some_fields()
        {
            var producer = (Func<IActor>)(() => null);
            var supervisorStrategyMock = new DoNothingSupervisorStrategy();
            var middleware = new Receive(ctx => Actor.Done);
            var parent = new PID("test", "test");

            var context = new LocalContext(producer, supervisorStrategyMock, middleware, null, parent);

            Assert.Equal(parent, context.Parent);

            Assert.Null(context.Message);
            Assert.Null(context.Sender);
            Assert.Null(context.Self);
            Assert.Null(context.Actor);
            Assert.NotNull(context.Children);
            Assert.Same(context.Children, LocalContext.EmptyChildren);

            Assert.Equal(TimeSpan.Zero, context.ReceiveTimeout);
        }

        [Fact]
        public async Task ReenterAfter_Can_Do_Task()
        {
            var queue = new ConcurrentQueue<string>();
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message as string == "hello1")
                {
                    var t = Task.Run(async () =>
                    {
                        await Task.Yield();
                        await Task.Yield();
                        queue.Enqueue("bar");
                        await Task.Yield();
                    });
                    ctx.ReenterAfter(t, task =>
                    {
                        queue.Enqueue("baz");
                        ctx.Respond("hey1");
                        return task;
                    });
                }
                else if (ctx.Message as string == "hello2")
                {
                    queue.Enqueue("foo");
                    ctx.Respond("hey2");
                }
                return Actor.Done;
            });

            var task1 = pid.RequestAsync<object>("hello1");
            var task2 = pid.RequestAsync<object>("hello2");
            await Task.Yield();
            var reply1 = await task1;
            var reply2 = await task2;

            Assert.Equal("hey1", reply1);
            Assert.Equal("hey2", reply2);

            string one;
            string two;
            string three;

            queue.TryDequeue(out one);
            queue.TryDequeue(out two);
            queue.TryDequeue(out three);

            Assert.Equal("foo", one);
            Assert.Equal("bar", two);
            Assert.Equal("baz", three);

        }
        [Fact]
        public async Task ReenterAfter_Can_Do_Task_T()
        {
            var queue = new ConcurrentQueue<string>();
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message as string == "hello1")
                {
                    var t = Task.Run(async () =>
                    {
                        await Task.Yield();
                        await Task.Yield();
                        queue.Enqueue("bar");
                        return "hey1";
                    });
                    ctx.ReenterAfter(t, task =>
                    {
                        queue.Enqueue("baz");
                        ctx.Respond(task.Result);
                        return task;
                    });
                }
                else if (ctx.Message as string == "hello2")
                {
                    queue.Enqueue("foo");
                    ctx.Respond("hey2");
                }
                return Actor.Done;
            });

            var task1 = pid.RequestAsync<object>("hello1");
            var task2 = pid.RequestAsync<object>("hello2");
            await Task.Yield();
            var reply1 = await task1;
            var reply2 = await task2;

            Assert.Equal("hey1", reply1);
            Assert.Equal("hey2", reply2);

            string one;
            string two;
            string three;

            queue.TryDequeue(out one);
            queue.TryDequeue(out two);
            queue.TryDequeue(out three);

            Assert.Equal("foo", one);
            Assert.Equal("bar", two);
            Assert.Equal("baz", three);

        }


    }
}
