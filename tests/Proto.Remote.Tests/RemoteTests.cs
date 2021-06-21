using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Proto.Remote.Tests.Messages;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    [Collection("RemoteTests")]
    public abstract class RemoteTests
    {
        private readonly IRemoteFixture _fixture;

        protected RemoteTests(IRemoteFixture fixture) => _fixture = fixture;

        private ActorSystem System => _fixture.ActorSystem;
        private IRemote Remote => _fixture.Remote;

        [Fact, DisplayTestMethodName]
        public async Task CanSendAndReceiveToExistingRemote()
        {
            var remoteActor = PID.FromAddress(_fixture.RemoteAddress, "EchoActorInstance");

            var pong = await System.Root.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                TimeSpan.FromMilliseconds(5000)
            );

            Assert.Equal($"{_fixture.RemoteAddress} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async Task WhenRemoteActorNotFound_DeadLetterException()
        {
            var unknownRemoteActor = PID.FromAddress(_fixture.RemoteAddress, "doesn't exist");

            await Assert.ThrowsAsync<DeadLetterException>(
                async () => {
                    await System.Root.RequestAsync<Pong>(unknownRemoteActor, new Ping {Message = "Hello"},
                        TimeSpan.FromMilliseconds(2000)
                    );
                }
            );
        }

        [Fact, DisplayTestMethodName]
        public async Task CanSpawnRemoteActor()
        {
            var remoteActorName = Guid.NewGuid().ToString();

            var remoteActorResp = await Remote.SpawnNamedAsync(
                _fixture.RemoteAddress, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5)
            );
            var remoteActor = remoteActorResp.Pid;
            var pong = await System.Root.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                TimeSpan.FromMilliseconds(5000)
            );
            Assert.Equal($"{_fixture.RemoteAddress} Hello", pong.Message);
        }

        [Fact, DisplayTestMethodName]
        public async Task CanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);

            System.Root.Stop(remoteActor);

            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );
        }

        [Fact, DisplayTestMethodName]
        public async Task CanWatchMultipleRemoteActors()
        {
            var remoteActor1 = await SpawnRemoteActor(_fixture.RemoteAddress);
            var remoteActor2 = await SpawnRemoteActor(_fixture.RemoteAddress);
            var localActor = await SpawnLocalActorAndWatch(remoteActor1, remoteActor2);

            System.Root.Stop(remoteActor1);
            System.Root.Stop(remoteActor2);

            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor1.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );

            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor2.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );
        }

        [Fact, DisplayTestMethodName]
        public async Task MultipleLocalActorsCanWatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            System.Root.Stop(remoteActor);

            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor1, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );

            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor2, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );
        }

        [Fact, DisplayTestMethodName]
        public async Task CanUnwatchRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);
            var localActor1 = await SpawnLocalActorAndWatch(remoteActor);
            var localActor2 = await SpawnLocalActorAndWatch(remoteActor);
            System.Root.Send(localActor2, new Unwatch(remoteActor));
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for unwatch to propagate...
            System.Root.Stop(remoteActor);

            // localActor1 is still watching so should get notified
            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(
                            localActor1, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );

            // localActor2 is NOT watching so should not get notified
            Assert.False(
                await System.Root.RequestAsync<bool>(
                    localActor2, new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                    TimeSpan.FromSeconds(5)
                ),
                "Unwatch did not succeed."
            );
        }

        [Fact, DisplayTestMethodName]
        public async Task WhenRemoteTerminated_LocalWatcherReceivesNotification()
        {
            var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);
            var localActor = await SpawnLocalActorAndWatch(remoteActor);

            System.Root.Send(remoteActor, new Die());
            Assert.True(
                await PollUntilTrue(
                    () =>
                        System.Root.RequestAsync<bool>(localActor,
                            new TerminatedMessageReceived(_fixture.RemoteAddress, remoteActor.Id),
                            TimeSpan.FromSeconds(5)
                        )
                ),
                "Watching actor did not receive Termination message"
            );
            Assert.Equal(1,
                await System.Root.RequestAsync<int>(localActor, new GetTerminatedMessagesCount(),
                    TimeSpan.FromSeconds(5)
                )
            );
        }
        
        [Fact, DisplayTestMethodName]
        public async Task CanMakeRequestToRemoteActor()
        {
            var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

            var res = await System.Root.RequestAsync<Touched>(remoteActor, new Touch(), TimeSpan.FromSeconds(5));
            res.Who.Should().BeEquivalentTo(remoteActor);
        }

        [Fact]
        public async Task CanMakeBinaryRequestToRemoteActor()
        {
            _fixture.LogStore.Clear();

            if (_fixture is HostedGrpcNetWithCustomSerializerTests.Fixture)
            {
                //Skip 
                return;
            }
            

            var remoteActorName = Guid.NewGuid().ToString();

            var remoteActorResp = await Remote.SpawnNamedAsync(
                _fixture.RemoteAddress, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5)
            );
            var remoteActor = remoteActorResp.Pid;
            var msg = new BinaryMessage()
            {
                Id = "hello"
            };
            
            var res = await System.Root.RequestAsync<Ack>(remoteActor, msg,
                CancellationTokens.FromSeconds(5)
            );
            res.Should().BeOfType<Ack>();

            var log = _fixture.LogStore.ToFormattedString();
        }

        private async Task<PID> SpawnRemoteActor(string address)
        {
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActorResp =
                await Remote.SpawnNamedAsync(address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            return remoteActorResp.Pid;
        }

        private async Task<PID> SpawnLocalActorAndWatch(params PID[] remoteActors)
        {
            var props = Props.FromProducer(() => new LocalActor(remoteActors));
            var actor = System.Root.Spawn(props);

            // The local actor watches the remote one - we wait here for the RemoteWatch 
            // message to propagate to the remote actor
            var logger = Log.CreateLogger(nameof(SpawnLocalActorAndWatch));
            logger.LogInformation("Waiting for RemoteWatch to propagate...");
            await Task.Delay(20);
            return actor;
        }

        private Task<bool> PollUntilTrue(Func<Task<bool>> predicate) =>
            PollUntilTrue(predicate, 100, TimeSpan.FromMilliseconds(50));

        private async Task<bool> PollUntilTrue(Func<Task<bool>> predicate, int attempts, TimeSpan interval)
        {
            var logger = Log.CreateLogger(nameof(PollUntilTrue));
            var attempt = 1;

            while (attempt <= attempts)
            {
                logger.LogInformation($"Attempting assertion (attempt {attempt} of {attempts})");

                if (await predicate())
                {
                    logger.LogInformation("Passed!");
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

    public class GetTerminatedMessagesCount
    {
    }

    public class LocalActor : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<LocalActor>();
        private readonly List<PID> _remoteActors = new();
        private readonly List<Terminated> _terminatedMessages = new();

        public LocalActor(params PID[] remoteActors) => _remoteActors.AddRange(remoteActors);

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

            return Task.CompletedTask;
        }

        private void HandleCountOfMessagesReceived(IContext context) => context.Respond(_terminatedMessages.Count);

        private void HandleTerminatedMessageReceived(IContext context, TerminatedMessageReceived msg)
        {
            var messageReceived = _terminatedMessages.Any(
                tm => tm.Who.Address == msg.Address &&
                      tm.Who.Id == msg.ActorId
            );
            context.Respond(messageReceived);
        }

        private void HandleTerminated(Terminated msg)
        {
            _logger.LogInformation(
                $"Received Terminated message for {msg.Who.Address}: {msg.Who.Id}. Reason? {msg.Why}"
            );
            _terminatedMessages.Add(msg);
        }

        private void HandleUnwatch(IContext context, Unwatch msg)
        {
            var remoteActor = _remoteActors.Single(
                ra => ra.Id == msg.Watcher.Id &&
                      ra.Address == msg.Watcher.Address
            );

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