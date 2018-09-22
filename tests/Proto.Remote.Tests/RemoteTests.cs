using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests
{
    
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class RemoteTests
    {
        private static readonly RootContext Context = new RootContext();
        private readonly RemoteManager _remoteManager;

        public RemoteTests(RemoteManager remoteManager)
        {
            _remoteManager = remoteManager;
        }

        [Fact, DisplayTestMethodName]
        public void CanSerializeAndDeserializeJsonPID()
        {

            var typeName = "actor.PID";
            var json = new JsonMessage(typeName, "{ \"Address\":\"123\", \"Id\":\"456\"}");
            var bytes = Serialization.Serialize(json, 1);
            var deserialized = Serialization.Deserialize(typeName, bytes, 1) as PID;
            Assert.Equal("123", deserialized.Address);
            Assert.Equal("456", deserialized.Id);
        }


        [Fact, DisplayTestMethodName]
        public void CanSerializeAndDeserializeJson()
        {

            var typeName = "remote_test_messages.Ping";
            var json = new JsonMessage(typeName, "{ \"message\":\"Hello\"}");
            var bytes = Serialization.Serialize(json, 1);
            var deserialized = Serialization.Deserialize(typeName, bytes, 1) as Ping;
            Assert.Equal("Hello",deserialized.Message);
        }

        [Fact, DisplayTestMethodName]
        public async void CanSendJsonAndReceiveToExistingRemote()
        {
            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var ct = new CancellationTokenSource(3000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            var localActor = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is Pong)
                {
                    tcs.SetResult(true);
                    ctx.Self.Stop();
                }

                return Actor.Done;
            }));
            
            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            Remote.SendMessage(remoteActor, envelope, 1);
            await tcs.Task;
        }

        [Fact, DisplayTestMethodName]
        public async void CanSendAndReceiveToExistingRemote()
        {
            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var pong = await Context.RequestAsync<Pong>(remoteActor, new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async void WhenRemoteActorNotFound_RequestAsyncTimesout()
        {
            var unknownRemoteActor = new PID(_remoteManager.DefaultNode.Address, "doesn't exist");
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await Context.RequestAsync<Pong>(unknownRemoteActor, new Ping { Message = "Hello" }, TimeSpan.FromMilliseconds(2000));
            });
        }

        [Fact, DisplayTestMethodName]
        public async void CanSpawnRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActorResp = await Remote.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var remoteActor = remoteActorResp.Pid;
            var pong = await Context.RequestAsync<Pong>(remoteActor, new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async void CanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);
            
            remoteActor.Stop();
            Assert.True(await PollUntilTrue(() => 
                    Context.RequestAsync<bool>(localActor, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async void CanWatchMultipleRemoteActors()
        {
            var remoteActor1 = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var remoteActor2 = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor1, remoteActor2);

            remoteActor1.Stop();
            remoteActor2.Stop();
            Assert.True(await PollUntilTrue(() => 
                    Context.RequestAsync<bool>(localActor, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor1.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
            Assert.True(await PollUntilTrue(() =>
                    Context.RequestAsync<bool>(localActor, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor2.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async void MultipleLocalActorsCanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);

            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            remoteActor.Stop();

            Assert.True(await PollUntilTrue(() =>
                    Context.RequestAsync<bool>(localActor1, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
            Assert.True(await PollUntilTrue(() =>
                    Context.RequestAsync<bool>(localActor2, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))),
                "Watching actor did not receive Termination message");
        }

        [Fact, DisplayTestMethodName]
        public async void CanUnwatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_remoteManager.DefaultNode.Address);
            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            Context.Send(localActor2, new Unwatch(remoteActor));
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for unwatch to propagate...
            remoteActor.Stop();

            // localActor1 is still watching so should get notified
            Assert.True(await PollUntilTrue(() => 
                    Context.RequestAsync<bool>(localActor1, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
            // localActor2 is NOT watching so should not get notified
            Assert.False(await Context.RequestAsync<bool>(localActor2, new TerminatedMessageReceived(_remoteManager.DefaultNode.Address, remoteActor.Id), TimeSpan.FromSeconds(5)), 
                "Unwatch did not succeed.");
        }

        [Fact, DisplayTestMethodName]
        public async void WhenRemoteTerminated_LocalWatcherReceivesNotification()
        {
            var (address, process) = _remoteManager.ProvisionNode("127.0.0.1", 12002);
            
            var remoteActor = await SpawnRemoteActor(address);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);
            Console.WriteLine($"Killing remote process {address}!");
            process.Kill();
            Assert.True(await PollUntilTrue(() => 
                    Context.RequestAsync<bool>(localActor, new TerminatedMessageReceived(address, remoteActor.Id), TimeSpan.FromSeconds(5))), 
                "Watching actor did not receive Termination message");
            Assert.Equal(1, await Context.RequestAsync<int>(localActor, new GetTerminatedMessagesCount(), TimeSpan.FromSeconds(5)));
        }

        private static async Task<PID> SpawnRemoteActor(string address)
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActorResp = await Remote.SpawnNamedAsync(address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            return remoteActorResp.Pid;
        }

        private async Task<PID> SpawnLocalActorAndWatch(params PID[] remoteActors)
        {
            var props = Props.FromProducer(() => new LocalActor(remoteActors));
            var actor = Context.Spawn(props);
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
                    HandleStarted(context);
                    break;
                case Unwatch msg:
                    HandleUnwatch(context, msg);
                    break;
                case TerminatedMessageReceived msg:
                    HandleTerminatedMessageReceived(context, msg);
                    break;
                case GetTerminatedMessagesCount _:
                    HandleCountOfMessagesReceived(context);
                    break;
                case Terminated msg:
                    HandleTerminated(msg);
                    break;
            }
            return Actor.Done;
        }

        private void HandleCountOfMessagesReceived(IContext context)
        {
            context.Respond(_terminatedMessages.Count);
        }

        private void HandleTerminatedMessageReceived(IContext context, TerminatedMessageReceived msg)
        {
            var messageReceived = _terminatedMessages.Any(tm => tm.Who.Address == msg.Address &&
                                                                tm.Who.Id == msg.ActorId);
            context.Respond(messageReceived);
        }

        private void HandleTerminated(Terminated msg)
        {
            Console.WriteLine($"Received Terminated message for {msg.Who.Address}: {msg.Who.Id}. Address terminated? {msg.AddressTerminated}");
            _terminatedMessages.Add(msg);
        }

        private void HandleUnwatch(IContext context, Unwatch msg)
        {
            var remoteActor =_remoteActors.Single(ra => ra.Id == msg.Watcher.Id && 
                                                        ra.Address == msg.Watcher.Address);

            context.Unwatch(remoteActor);
        }

        private void HandleStarted(IContext context)
        {
            foreach (var remoteActor in _remoteActors)
            {
                context.Watch(remoteActor);
            }
        }
    }
}
