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
        public async Task CanWatchLocalActors()
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
                        return context.WatchAsync(_watchee);
                    case string msg when msg == "?":
                        return context.Sender.SendAsync(_terminateReceived);
                    case Terminated msg:
                        _terminateReceived = true;
                        break;
                }
                return Actor.Done;
            }
        }
    }
}
