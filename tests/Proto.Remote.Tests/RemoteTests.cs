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
        private readonly RemoteManager _remoteManager;

        public RemoteTests(RemoteManager remoteManager)
        {
            _remoteManager = remoteManager;
        }

        [Fact]
        public async void CanSendAndReceiveRemote()
        {
            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var pong = await remoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact]
        public async void WhenRemoteActorNotFound_RequestAsyncTimesout()
        {
            var unknownRemoteActor = new PID(_remoteManager.DefaultNode.Address, "doesn't exist");
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await unknownRemoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(2000));
            });
        }

        [Fact]
        public async void CanSpawnRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var pong = await remoteActor.RequestAsync<Pong>(new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact]
        public async void CanWatchRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));

            var props = Actor.FromProducer(() => new LocalActor(remoteActor));
            var localActor = Actor.Spawn(props);
            remoteActor.Stop();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for stop to propagate...
            var terminatedMessageReceived = await localActor.RequestAsync<bool>("?", TimeSpan.FromSeconds(5));
            Assert.True(terminatedMessageReceived);
        }

        [Fact]
        public async void WhenRemoteTerminated_LocalWatcherReceivesNotification()
        {
            var (address, process) = _remoteManager.StartRemote("127.0.0.1", 12002);
            await Task.Delay(TimeSpan.FromSeconds(3));
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync(address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));

            var props = Actor.FromProducer(() => new LocalActor(remoteActor));
            var localActor = Actor.Spawn(props);
            process.Kill();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for kill to propagate...
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
                case Started _:
                    context.Watch(_remoteActor);
                    break;
                case string msg when msg == "?":
                    context.Sender.Tell(_terminateReceived);
                    break;
                case Terminated _:
                    _terminateReceived = true;
                    break;
            }
            return Actor.Done;
        }
    }
}
