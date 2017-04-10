using System;
using Xunit;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests
{
    [Collection("RemoteTests")]
    [Trait("Category", "Remote")]
    public class RemoteTests
    {
        [Fact]
        public async void CanSendAndReceiveRemote()
        {
            var props = Actor.FromProducer(() => new LocalActor());
            var pid = Actor.Spawn(props);
            
            var remoteActor = new PID("127.0.0.1:12000", "remote");
            await remoteActor.RequestAsync<Start>(new StartRemote { Sender = pid }, TimeSpan.FromMilliseconds(2500));

            remoteActor.Tell(new Ping());
            await Task.Delay(TimeSpan.FromSeconds(2));
            var responseReceived = await pid.RequestAsync<bool>("?", TimeSpan.FromMilliseconds(2500));

            Assert.True(responseReceived);
        }

        [Fact]
        public async void WhenRemoteActorNotFound_RequestAsyncTimesout()
        {
            var props = Actor.FromProducer(() => new LocalActor());
            var pid = Actor.Spawn(props);

            var unknownRemoteActor = new PID("127.0.0.1:12000", "doesn't exist");
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await unknownRemoteActor.RequestAsync<Start>(new StartRemote { Sender = pid }, TimeSpan.FromMilliseconds(2000));
            });
        }

        [Fact]
        public async void CanSpawnRemoteActor()
        {
            var props = Actor.FromProducer(() => new LocalActor());
            var pid = Actor.Spawn(props);
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync("127.0.0.1:12000", remoteActorName, "remote", TimeSpan.FromSeconds(5));
            await remoteActor.RequestAsync<Start>(new StartRemote { Sender = pid }, TimeSpan.FromMilliseconds(2500));
            remoteActor.Tell(new Ping());
            await Task.Delay(TimeSpan.FromSeconds(2));

            var responseReceived = await pid.RequestAsync<bool>("?", TimeSpan.FromMilliseconds(2500));
            Assert.True(responseReceived);
        }

        public class LocalActor : IActor
        {
            private bool _pongReceived;

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Pong _:
                        _pongReceived = true;
                        break;
                    case string msg when msg == "?":
                        context.Sender.Tell(_pongReceived);
                        break;
                }
                return Actor.Done;
            }
        }
    }
}
