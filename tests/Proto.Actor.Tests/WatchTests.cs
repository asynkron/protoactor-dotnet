﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class WatchTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        [Fact]
        public async Task MultipleStopsTriggerSingleTerminated()
        {
            long counter = 0;
            var childProps = Props.FromFunc(context => {
                    switch (context.Message)
                    {
                        case Started _:
                            context.Stop(context.Self);
                            context.Stop(context.Self);
                            break;
                    }

                    return Task.CompletedTask;
                }
            );

            Context.Spawn(Props.FromFunc(context => {
                        switch (context.Message)
                        {
                            case Started _:
                                context.Spawn(childProps);
                                break;
                            case Terminated _:
                                Interlocked.Increment(ref counter);
                                break;
                        }

                        return Task.CompletedTask;
                    }
                )
            );

            await Task.Delay(1000);
            Assert.Equal(1, Interlocked.Read(ref counter));
        }

        [Fact]
        public async Task CanWatchLocalActors()
        {
            var watchee = Context.Spawn(Props.FromProducer(() => new DoNothingActor())
                .WithMailbox(() => new TestMailbox())
            );
            var watcher = Context.Spawn(Props.FromProducer(() => new LocalActor(watchee))
                .WithMailbox(() => new TestMailbox())
            );

            await Context.StopAsync(watchee);
            var terminatedMessageReceived = await Context.RequestAsync<bool>(watcher, "?", TimeSpan.FromSeconds(5));
            Assert.True(terminatedMessageReceived);
        }

        public class LocalActor : IActor
        {
            private readonly PID _watchee;
            private bool _terminateReceived;

            public LocalActor(PID watchee) => _watchee = watchee;

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        context.Watch(_watchee);
                        break;
                    case string msg when msg == "?":
                        context.Respond(_terminateReceived);
                        break;
                    case Terminated _:
                        _terminateReceived = true;
                        break;
                }

                return Task.CompletedTask;
            }
        }
    }
}