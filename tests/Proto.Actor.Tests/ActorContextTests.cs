using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class LocalContextTests
    {
        private static readonly RootContext Context = new RootContext();
        public static PID SpawnActorFromFunc(Receive receive) => Context.Spawn(Props.FromFunc(receive));

        [Fact]
        public void Given_Context_ctor_should_set_some_fields()
        {
            var parent = new PID("test", "test");
            var props = new Props()
                .WithProducer(() => null)
                .WithChildSupervisorStrategy(new DoNothingSupervisorStrategy())
                .WithReceiveMiddleware(next => (ctx,env) => Actor.Done);
            
            var context = new ActorContext(props, parent);

            Assert.Equal(parent, context.Parent);

            Assert.Null(context.Message);
            Assert.Null(context.Sender);
            Assert.Null(context.Self);
            Assert.Null(context.Actor);
            Assert.NotNull(context.Children);
            Assert.NotNull(context.Children);

            Assert.Equal(TimeSpan.Zero, context.ReceiveTimeout);
        }

        [Fact]
        public async Task ReenterAfter_Can_Do_Action_For_Task_T()
        {
            var queue = new ConcurrentQueue<string>();
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message as string == "hello1")
                {
                    var t = Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        queue.Enqueue("bar");
                        return "hey1";
                    });
                    ctx.ReenterAfter(t, () =>
                    {
                        queue.Enqueue("baz");
                        ctx.Respond(t.Result);
                    });
                }
                else if (ctx.Message as string == "hello2")
                {
                    queue.Enqueue("foo");
                    ctx.Respond("hey2");
                }
                return Actor.Done;
            });

            var task1 = Context.RequestAsync<object>(pid, "hello1");
            var task2 = Context.RequestAsync<object>(pid, "hello2");
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
