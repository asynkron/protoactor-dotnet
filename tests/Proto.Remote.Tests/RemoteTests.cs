using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Proto.Future;
using Proto.Remote.Tests.Messages;
using Proto.TestKit;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public abstract class RemoteTests
{
    private readonly IRemoteFixture _fixture;

    protected RemoteTests(IRemoteFixture fixture)
    {
        _fixture = fixture;
    }

    private ActorSystem System => _fixture.ActorSystem;
    private IRemote Remote => _fixture.Remote;

    [Fact]
    [DisplayTestMethodName]
    public async Task CanSendAndReceiveToExistingRemote()
    {
        var remoteActor = PID.FromAddress(_fixture.RemoteAddress, "EchoActorInstance");

        var pong = await System.Root.RequestAsync<Pong>(remoteActor, new Ping { Message = "Hello" },
            TimeSpan.FromSeconds(10)
        );

        Assert.Equal($"{_fixture.RemoteAddress} Hello", pong.Message);
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanForwardBetweenRemotes()
    {
        var remoteActor1 = PID.FromAddress(_fixture.RemoteAddress, "EchoActorInstance");
        var remoteActor2 = PID.FromAddress(_fixture.RemoteAddress2, "EchoActorInstance");

        var response = await System.Root.RequestAsync<ForwardResponse>(remoteActor1, new Forward
                { Message = "Hi", Target = remoteActor2 },
            TimeSpan.FromSeconds(10)
        );

        response.Should()
            .BeEquivalentTo(new ForwardResponse
                {
                    Message = "Hi", Sender = remoteActor2
                }
            );
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task RemoteHandlesRequestIdsCorrectly()
    {
        const int messageCount = 200;
        var timeout = new CancellationTokenSource(10000);
        var batchContext = _fixture.ActorSystem.Root.CreateBatchContext(messageCount, timeout.Token);
        // Batch futures are using the same process, but differentiate responses based on the request id
        var requestIds = Enumerable.Range(1, messageCount).ToList();

        List<(IFuture future, Ping message)> requests = requestIds
            .Select(i => (batchContext.GetFuture(), new Ping { Message = i.ToString() }))
            .ToList();

        var remoteActor = PID.FromAddress(_fixture.RemoteAddress, "EchoActorInstance");

        // Send all request at once, to make sure they are batched in the remote
        foreach (var request in requests)
        {
            System.Root.Request(remoteActor, request.message, request.future.Pid);
        }

        var responses = await Task.WhenAll(requests.Select(request => GetResponse(request.future)));

        for (var index = 0; index < requests.Count; index++)
        {
            // Make sure that each request got the matched response
            var ping = requests[index].message;
            var pong = responses[index];
            Assert.Equal($"{_fixture.RemoteAddress} " + ping.Message, pong.Message);
        }

        static async Task<Pong> GetResponse(IFuture future)
        {
            var response = await future.Task;

            return response switch
            {
                Proto.MessageEnvelope envelope => (Pong)envelope.Message,
                Pong pong                      => pong,
                _                              => throw new ArgumentException(response?.ToString(), nameof(response))
            };
        }
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task WhenRemoteActorNotFound_DeadLetterException()
    {
        var unknownRemoteActor = PID.FromAddress(_fixture.RemoteAddress, "doesn't exist");

        await Assert.ThrowsAsync<DeadLetterException>(
            async () =>
            {
                await System.Root.RequestAsync<Pong>(unknownRemoteActor, new Ping { Message = "Hello" },
                    TimeSpan.FromSeconds(10)
                );
            }
        );
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanSpawnRemoteActor()
    {
        var remoteActorName = Guid.NewGuid().ToString();

        var remoteActorResp = await Remote.SpawnNamedAsync(
            _fixture.RemoteAddress, remoteActorName, "EchoActor", TimeSpan.FromSeconds(10)
        );

        var remoteActor = remoteActorResp.Pid;

        var pong = await System.Root.RequestAsync<Pong>(remoteActor, new Ping { Message = "Hello" },
            TimeSpan.FromSeconds(10)
        );

        Assert.Equal($"{_fixture.RemoteAddress} Hello", pong.Message);
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanSpawnActorOnClientRemote()
    {
        var remoteActorName = Guid.NewGuid().ToString();

        var remoteActorResp = await Remote.SpawnNamedAsync(
            _fixture.RemoteAddress, remoteActorName, "EchoActor", TimeSpan.FromSeconds(10)
        );

        var remoteActor = remoteActorResp.Pid;

        var pong = await System.Root.RequestAsync<SpawnOnMeAndPingResponse>(remoteActor, new SpawnOnMeAndPing(),
            TimeSpan.FromSeconds(10)
        );

        Assert.Equal($"{_fixture.ActorSystem.Address} Hello", pong.Message);
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanWatchRemoteActor()
    {
        var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

        var probe = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe));
        await Task.Delay(20); // wait for probe to start
        probe.Context.Watch(remoteActor);
        await Task.Delay(20); // allow RemoteWatch to propagate

        System.Root.Stop(remoteActor);

        var terminated = await probe.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        Assert.Equal(remoteActor, terminated.Who);
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanWatchMultipleRemoteActors()
    {
        var remoteActor1 = await SpawnRemoteActor(_fixture.RemoteAddress);
        var remoteActor2 = await SpawnRemoteActor(_fixture.RemoteAddress);

        var probe = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe));
        await Task.Delay(20); // wait for probe to start
        probe.Context.Watch(remoteActor1);
        probe.Context.Watch(remoteActor2);
        await Task.Delay(20); // allow RemoteWatch to propagate

        System.Root.Stop(remoteActor1);
        System.Root.Stop(remoteActor2);

        var term1 = await probe.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        var term2 = await probe.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        new[] { term1.Who, term2.Who }.Should().BeEquivalentTo(new[] { remoteActor1, remoteActor2 });
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task MultipleLocalActorsCanWatchRemoteActor()
    {
        var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

        var probe1 = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe1));
        var probe2 = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe2));
        await Task.Delay(20);
        probe1.Context.Watch(remoteActor);
        probe2.Context.Watch(remoteActor);
        await Task.Delay(20);

        System.Root.Stop(remoteActor);

        var t1 = await probe1.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        var t2 = await probe2.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        Assert.Equal(remoteActor, t1.Who);
        Assert.Equal(remoteActor, t2.Who);
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanUnwatchRemoteActor()
    {
        var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

        var probe1 = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe1));
        var probe2 = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe2));
        await Task.Delay(20);
        probe1.Context.Watch(remoteActor);
        probe2.Context.Watch(remoteActor);
        await Task.Delay(20);

        probe2.Context.Unwatch(remoteActor);
        await Task.Delay(TimeSpan.FromSeconds(3));

        System.Root.Stop(remoteActor);

        var term = await probe1.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        Assert.Equal(remoteActor, term.Who);

        await probe2.ExpectNoMessageAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task WhenRemoteTerminated_LocalWatcherReceivesNotification()
    {
        var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

        var probe = new TestProbe();
        System.Root.Spawn(Props.FromProducer(() => probe));
        await Task.Delay(20);
        probe.Context.Watch(remoteActor);
        await Task.Delay(20);

        System.Root.Send(remoteActor, new Die());

        var term = await probe.ExpectSystemMessageAsync<Terminated>(TimeSpan.FromSeconds(10));
        Assert.Equal(remoteActor, term.Who);

        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    [DisplayTestMethodName]
    public async Task CanMakeRequestToRemoteActor()
    {
        var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);

        var res = await System.Root.RequestAsync<Touched>(remoteActor, new Touch(), TimeSpan.FromSeconds(10));
        res.Who.Should().BeEquivalentTo(remoteActor);
    }

    [Theory]
    [DisplayTestMethodName]
    [InlineData(true, 1, 1, 5)]
    [InlineData(true, 2, 1, 5)]
    [InlineData(true, 10, 100, 5)]
    [InlineData(false, 1, 1, 5)]
    [InlineData(false, 2, 1, 5)]
    [InlineData(false, 10, 100, 5)]
    public async Task ConcurrentMessagesWorks(bool remote, int messageCount, int messageSize, int timeoutSeconds)
    {
        if (_fixture is HostedGrpcNetWithCustomSerializerTests.Fixture)
        {
            //Skip 
            return;
        }

        // Seeded for deterministic message payloads
        var rnd = new Random(0);
        var tcs = new TaskCompletionSource<bool>();
        long responseCount = 0;

        var responseHandler = _fixture.ActorSystem.Root.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is Ack)
                    {
                        if (Interlocked.Increment(ref responseCount) == messageCount)
                        {
                            tcs.TrySetResult(true);
                        }
                    }

                    return Task.CompletedTask;
                }
            )
        );

        var actor = remote ? await SpawnRemoteActor(_fixture.RemoteAddress) : SpawnLocalActor();

        var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

        for (var i = 0; i < messageCount; i++)
        {
            System.Root.Request(actor, NextMsg(), responseHandler);
        }

        await Task.WhenAny(tcs.Task, timeout);

        var res = await System.Root.RequestAsync<Touched>(actor, new Touch(), TimeSpan.FromSeconds(1));
        res.Should().NotBeNull("Remote should still be alive");
        res.Who.Should().BeEquivalentTo(actor);

        Interlocked.Read(ref responseCount).Should().Be(messageCount);

        tcs.Task.IsCompletedSuccessfully.Should().BeTrue("All responses received");
        Interlocked.Read(ref responseCount).Should().Be(messageCount);

        BinaryMessage NextMsg()
        {
            var bytes = new byte[messageSize];
            rnd.NextBytes(bytes);

            return new BinaryMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Payload = ByteString.CopyFrom(bytes)
            };
        }
    }
    //
    // [Fact]
    // public async Task CanBlockRemote()
    // {
    //     _fixture.LogStore.Clear();
    //     var remoteId = _fixture.ServerRemote1.System.Id;
    //     System.Remote().BlockList.Block(new []{remoteId});
    //     
    //     
    //     var remoteActor = await SpawnRemoteActor(_fixture.RemoteAddress);
    //
    //     System.Root.Send(remoteActor, new Die());
    // }

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
            _fixture.RemoteAddress, remoteActorName, "EchoActor", TimeSpan.FromSeconds(10)
        );

        var remoteActor = remoteActorResp.Pid;

        var msg = new BinaryMessage
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
            await Remote.SpawnNamedAsync(address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(10));

        return remoteActorResp.Pid;
    }

    private PID SpawnLocalActor() => System.Root.Spawn(RemoteFixture.EchoActorProps);
}