using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Future;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests;

public class LargeMessageEnvelopeTests : IClassFixture<GrpcNetClientWithHostedGrpcNetServerTests.Fixture>
{
    private readonly GrpcNetClientWithHostedGrpcNetServerTests.Fixture _fixture;

    private ActorSystem System => _fixture.ActorSystem;

    public LargeMessageEnvelopeTests(GrpcNetClientWithHostedGrpcNetServerTests.Fixture fixture) =>
        _fixture = fixture;

    [Fact]
    [DisplayTestMethodName]
    public async Task SendingManyLargeMessages_ShouldFailCommunication()
    {
        var remotePid = PID.FromAddress(_fixture.RemoteAddress, "EchoActorInstance");

        const int messageCount = 10;
        var largePayload = new string('x', 1024 * 1024); // 1 MB per message
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var batchContext = System.Root.CreateBatchContext(messageCount, timeout.Token);

        List<(IFuture future, Ping msg)> requests = Enumerable.Range(0, messageCount)
            .Select(_ => (batchContext.GetFuture(), new Ping { Message = largePayload }))
            .ToList();

        foreach (var req in requests)
        {
            System.Root.Request(remotePid, req.msg, req.future.Pid);
        }

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Task.WhenAll(requests.Select(r => r.future.Task)));
    }
}

