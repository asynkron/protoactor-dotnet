using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class WatchTests
    {
        [Fact]
        public async Task MultipleStopsTriggerSingleTerminated()
        {
            int counter = 0;
            var childProps = Actor.FromFunc(context =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.Self.Stop();
                        context.Self.Stop();
                        break;
                }
                return Actor.Done;
            });

            Actor.Spawn(Actor.FromFunc(context =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.Spawn(childProps);
                        break;
                    case Terminated t:
                        Interlocked.Increment(ref counter);
                        break;
                }
                return Actor.Done;
            }));

            await Task.Delay(1000);
            Assert.Equal(1,counter);

        }
        [Fact]
        public async void CanWatchLocalActors()
        {
            var watchee = Actor.Spawn(Actor.FromProducer(() => new DoNothingActor())
                                           .WithMailbox(() => new TestMailbox()));
            var watcher = Actor.Spawn(Actor.FromProducer(() => new LocalActor(watchee))
                                           .WithMailbox(() => new TestMailbox()));

            await watchee.StopAsync();
            var terminatedMessageReceived = await watcher.RequestAsync<bool>("?", TimeSpan.FromSeconds(5));
            Assert.True(terminatedMessageReceived);
        }

        public class LocalActor : IActor
        {
            private readonly PID _watchee;
            private bool _terminateReceived;

            public LocalActor(PID watchee)
            {
                _watchee = watchee;
            }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        context.Watch(_watchee);
                        break;
                    case string msg when msg == "?":
                        context.Sender.Tell(_terminateReceived);
                        break;
                    case Terminated msg:
                        _terminateReceived = true;
                        break;
                }
                return Actor.Done;
            }
        }
    }
}
