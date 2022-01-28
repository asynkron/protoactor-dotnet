using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class FutureTests : ActorTestBase
    {
        private readonly ITestOutputHelper _output;

        public FutureTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond("hey");
                        return Task.CompletedTask;
                    }
                )
            );

            var reply = Context.RequestAsync<object>(pid, "hello").Result;

            Assert.Equal("hey", reply);
        }

        [Fact]
        public void Given_Actor_When_AwaitContextRequestAsync_Should_GetReply()
        {
            var pid1 = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond("hey");
                        return Task.CompletedTask;
                    }
                )
            );
            var pid2 = Context.Spawn(Props.FromFunc(async ctx => {
                        if (ctx.Message is string)
                        {
                            var reply1 = await ctx.RequestAsync<string>(pid1, "");
                            ctx.Respond(ctx.Message + reply1);
                        }
                    }
                )
            );

            var reply2 = Context.RequestAsync<string>(pid2, "hello").Result;

            Assert.Equal("hellohey", reply2);
        }

        [Fact]
        public void Given_Actor_When_ReplyIsNull_Should_Return()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string) ctx.Respond(null!);
                        return Task.CompletedTask;
                    }
                )
            );

            var reply = Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Result;

            Assert.Null(reply);
        }

        [Fact]
        public void TestInATask() => SafeTask.Run(async () => {
                var pid = Context.Spawn(Props.FromFunc(ctx => {
                            if (ctx.Message is string msg)
                            {
                                _output.WriteLine("Got Message " + msg);
                                ctx.Respond(null!);
                                _output.WriteLine("Sent Response to " + msg);
                            }

                            return Task.CompletedTask;
                        }
                    )
                );

                _output.WriteLine("Starting");
                var reply1 = await Context.RequestAsync<object>(pid, "hello1", TimeSpan.FromSeconds(10));
                Assert.Null(reply1);
                _output.WriteLine("got response 1");
                var reply2 = Context.RequestAsync<object>(pid, "hello2", TimeSpan.FromSeconds(10)).Result;
                Assert.Null(reply2);
                _output.WriteLine("got response 2");
            }
        ).Wait();

        [Fact]
        public void TestInATaskIndirect() => Task.Run(async () => {
                var replier = Context.Spawn(Props.FromFunc(ctx => {
                            if (ctx.Message is Tuple<PID, string> msg)
                            {
                                _output.WriteLine("replier Got Message " + msg.Item2);
                                msg.Item1.SendUserMessage(System, null!);
                                _output.WriteLine("replier Sent Response to " + msg.Item2);
                            }

                            return Task.CompletedTask;
                        }
                    )
                );
                var pid = Context.Spawn(Props.FromFunc(ctx => {
                            if (ctx.Message is string msg)
                            {
                                _output.WriteLine("pid Got Message " + msg);
                                replier.SendUserMessage(System, Tuple.Create(ctx.Sender, msg));
                                _output.WriteLine("pid Sent Response to " + msg);
                            }

                            return Task.CompletedTask;
                        }
                    )
                );

                _output.WriteLine("Starting");
                var reply1 = await Context.RequestAsync<object>(pid, "hello1", TimeSpan.FromSeconds(2));
                Assert.Null(reply1);
                _output.WriteLine("got response 1");
                var reply2 = Context.RequestAsync<object>(pid, "hello2", TimeSpan.FromSeconds(2)).Result;
                Assert.Null(reply2);
                _output.WriteLine("got response 2");
            }
        ).Wait();
    }
}