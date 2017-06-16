using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests
{
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class RemoteTests
    {
        private readonly RemoteManager _remoteManager;

        public RemoteTests(RemoteManager remoteManager)
        {
            _remoteManager = remoteManager;
        }

        [Fact, DisplayTestMethodName]
        public async Task CanSendAndReceiveToExistingRemote()
        {
            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var pong = await remoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async Task WhenRemoteActorNotFound_RequestAsyncTimesout()
        {
            var unknownRemoteActor = new PID(_remoteManager.DefaultNode.Address, "doesn't exist");
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await unknownRemoteActor.RequestAsync<Pong>(new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(2000));
            });
        }

        [Fact, DisplayTestMethodName]
        public async Task CanSpawnRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActor = await Remote.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var pong = await remoteActor.RequestAsync<Pong>(new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async Task CanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);
            
            await remoteActor.StopAsync();
            Assert.True(await PollUntilTrue(() => 
            localActor.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async Task CanWatchMultipleRemoteActors()
        {
            var remoteActor1 = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var remoteActor2 = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor1, remoteActor2);

            await remoteActor1.StopAsync();
            await remoteActor2.StopAsync();
            Assert.True(await PollUntilTrue(() => 
            localActor.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor1.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
            Assert.True(await PollUntilTrue(() =>
                    localActor.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor2.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async Task MultipleLocalActorsCanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);

            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            await remoteActor.StopAsync();

            Assert.True(await PollUntilTrue(() =>
                    localActor1.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
            Assert.True(await PollUntilTrue(() =>
                    localActor2.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async Task CanUnwatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            await localActor2.SendAsync(new Unwatch(remoteActor));
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for unwatch to propagate...
            await remoteActor.StopAsync();

            // localActor1 is still watching so should get notified
            Assert.True(await PollUntilTrue(() => 
                localActor1.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
            // localActor2 is NOT watching so should not get notified
            Assert.False(await localActor2.RequestAsync<bool>(new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5)), 
                "Unwatch did not succeed.");
        }

        [Fact, DisplayTestMethodName]
        public async Task WhenRemoteTerminated_LocalWatcherReceivesNotification()
        {
            var (address, process) = _remoteManager.ProvisionNode("127.0.0.1", 12002);
            
            var remoteActor = await SpawnRemoteActor(address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);
            Console.WriteLine($"Killing remote process {address}!");
            process.Kill();
            Assert.True(await PollUntilTrue(() => 
            localActor.RequestAsync<bool>(new TerminatedMessageReceived(address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
            Assert.Equal(1, await localActor.RequestAsync<int>(new GetTerminatedMessagesCount(), TimeSpan.FromSeconds(5)));
        }

        private static async Task<PID> SpawnRemoteActor(string address)
        {
            var remoteActorName = Guid.NewGuid().ToString();
            return await Remote.SpawnNamedAsync(address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
        }

        private async Task<PID> SpawnLocalActorAndWatch(params PID[] remoteActors)
        {
            var props = Actor.FromProducer(() => new LocalActor(remoteActors));
            var actor = Actor.Spawn(props);
            // The local actor watches the remote one - we wait here for the RemoteWatch 
            // message to propagate to the remote actor
            Console.WriteLine("Waiting for RemoteWatch to propagate...");
            await Task.Delay(2000);
            return actor;
        }

        private Task<bool> PollUntilTrue(Func<Task<bool>> predicate)
        {
            return PollUntilTrue(predicate, 10, TimeSpan.FromMilliseconds(500));
        }

        private async Task<bool> PollUntilTrue(Func<Task<bool>> predicate, int attempts, TimeSpan interval)
        {
            var attempt = 1;
            while (attempt <= attempts)
            {
                Console.WriteLine($"Attempting assertion (attempt {attempt} of {attempts})");
                if (await predicate())
                {
                    Console.WriteLine($"Passed!");
                    return true;
                }
                attempt++;
                await Task.Delay(interval);
            }
            return false;
        }
    }

    public class TerminatedMessageReceived
    {
        public TerminatedMessageReceived(string address, string actorId)
        {
            Address = address;
            ActorId = actorId;
        }
        public string Address { get; }
        public string ActorId { get; }
    }

    public class GetTerminatedMessagesCount { }

    public class LocalActor : IActor
    {
        private readonly List<PID> _remoteActors = new List<PID>();
        private readonly List<Terminated> _terminatedMessages = new List<Terminated>();

        public LocalActor(params PID[] remoteActors)
        {
            _remoteActors.AddRange(remoteActors);
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    return HandleStarted(context);
                case Unwatch msg:
                    return HandleUnwatch(context, msg);
                case TerminatedMessageReceived msg:
                    return HandleTerminatedMessageReceived(context, msg);
                case GetTerminatedMessagesCount _:
                    return HandleCountOfMessagesReceived(context);
                case Terminated msg:
                    HandleTerminated(msg);
                    break;
            }
            return Actor.Done;
        }

        private Task HandleCountOfMessagesReceived(IContext context)
        {
            return context.Sender.SendAsync(_terminatedMessages.Count);
        }

        private Task HandleTerminatedMessageReceived(IContext context, TerminatedMessageReceived msg)
        {
            var messageReceived = _terminatedMessages.Any(tm => tm.Who.Address == msg.Address &&
                                                                tm.Who.Id == msg.ActorId);
            return context.Sender.SendAsync(messageReceived);
        }

        private void HandleTerminated(Terminated msg)
        {
            Console.WriteLine($"Received Terminated message for {msg.Who.Address}: {msg.Who.Id}. Address terminated? {msg.AddressTerminated}");
            _terminatedMessages.Add(msg);
        }

        private Task HandleUnwatch(IContext context, Unwatch msg)
        {
            var remoteActor =_remoteActors.Single(ra => ra.Id == msg.Watcher.Id && 
                                                        ra.Address == msg.Watcher.Address);

            return context.UnwatchAsync(remoteActor);
        }

        private async Task HandleStarted(IContext context)
        {
            foreach (var remoteActor in _remoteActors)
            {
                await context.WatchAsync(remoteActor);
            }
        }
    }
}
