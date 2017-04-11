using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class WatchTests
    {
        [Fact]
        public async void CanWatchLocalActors()
        {
            var watchee = Actor.Spawn(Actor.FromProducer(() => new DoNothingActor())
                                           .WithMailbox(() => new TestMailbox()));
            var watcher = Actor.Spawn(Actor.FromProducer(() => new LocalActor(watchee))
                                           .WithMailbox(() => new TestMailbox()));

            watchee.Stop();
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
