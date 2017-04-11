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
            var remoteActor = new PID("127.0.0.1:12000", "remote");
            var pong = await remoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(5000));
            Assert.Equal("127.0.0.1:12000 Hello", pong.Message);
        }

        [Fact]
        public async void WhenRemoteActorNotFound_RequestAsyncTimesout()
        {
            var unknownRemoteActor = new PID("127.0.0.1:12000", "doesn't exist");
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await unknownRemoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(2000));
            });
        }

        [Fact]
        public async void CanSpawnRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync("127.0.0.1:12000", remoteActorName, "remote", TimeSpan.FromSeconds(5));
            var pong = await remoteActor.RequestAsync<Pong>(new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal("127.0.0.1:12000 Hello", pong.Message);
        }

        [Fact]
        public async void CanWatchRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync("127.0.0.1:12000", remoteActorName, "remote", TimeSpan.FromSeconds(5));

            var props = Actor.FromProducer(() => new LocalActor(remoteActor));
            var localActor = Actor.SpawnNamed(props, "local watcher");
            remoteActor.Stop();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for stop to propagate...
            var terminatedMessageReceived = await localActor.RequestAsync<bool>("?", TimeSpan.FromSeconds(5));
            Assert.True(terminatedMessageReceived);
        }
    }
 
    public class LocalActor : IActor
    {
        private readonly PID _remoteActor;
        private bool _terminateReceived;

        public LocalActor(PID remoteActor)
        {
            _remoteActor = remoteActor;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:
                    context.Watch(_remoteActor);
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
