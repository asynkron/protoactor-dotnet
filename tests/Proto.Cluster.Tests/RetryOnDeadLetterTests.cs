using System;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class RetryOnDeadLetterTests
{
    [Fact(Skip = "Flaky")]
    public async Task ShouldRetryRequestOnDeadLetterResponseRegardlessOfResponseType()
    {
        var fixture = new Fixture(1);
        await using var _ = fixture.ConfigureAwait(false);
        await fixture.InitializeAsync().ConfigureAwait(false);

        var member = fixture.Members.First();
        var identity = CreateIdentity("dead-letter-test");

        // make sure the actor is created and the PID is cached
        await member.RequestAsync<Pong>(identity, EchoActor.Kind, new Ping(), CancellationTokens.FromSeconds(1)).ConfigureAwait(false);

        // pretend we have an invalid PID in the cache
        var otherMember = await fixture.SpawnNode().ConfigureAwait(false);
        if (member.PidCache.TryGet(ClusterIdentity.Create(identity, EchoActor.Kind), out var pid))
        {
            var newPid = PID.FromAddress(otherMember.System.Address, pid.Id);
            if (!member.PidCache.TryUpdate(ClusterIdentity.Create(identity, EchoActor.Kind), newPid, pid))
            {
                Assert.Fail("Failed to replace actor's pid with a fake one in the pid cache");
            }
        }
        else
        {
            Assert.Fail("Did not find expected actor identity in the pid cache");
        }

        // check if the correct response type is returned
        var response = await member.RequestAsync<object>(identity, EchoActor.Kind, new Ping(), CancellationTokens.FromSeconds(1)).ConfigureAwait(false);
        response.Should().BeOfType<Pong>();

    }

    private string CreateIdentity(string baseId) => $"{Guid.NewGuid().ToString("N").Substring(0, 6)}-{baseId}-";
    
    private class Fixture : BaseInMemoryClusterFixture
    {
        public Fixture(int clusterSize)
            : base(clusterSize, cc => cc
                .WithActorRequestTimeout(TimeSpan.FromSeconds(1))
            )
        {
        }
    }
}