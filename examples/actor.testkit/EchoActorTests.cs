// -----------------------------------------------------------------------
// <copyright file="EchoActorTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using FluentAssertions;
using Proto.TestKit;
using Xunit;

namespace ActorTestKit;

public class EchoActorTests : IClassFixture<TestKitFixture>
{
    private readonly TestKitFixture _fixture;

    public EchoActorTests(TestKitFixture fixture) => _fixture = fixture;

    [Fact]
    public void Actor_replies_using_fixture_probe()
    {
        // Spawn the actor under test
        var pid = _fixture.Spawn<EchoActor>();

        // Send a message via the fixture's probe
        _fixture.Request(pid, new Ping("Proto"));

        // Probes capture messages and allow typed assertions
        var reply = _fixture.GetNextMessage<Pong>();
        reply.Who.Should().Be("Proto");
    }

    [Fact]
    public void Dedicated_probe_captures_messages()
    {
        // Create a separate probe
        TestProbe probe = _fixture.CreateTestProbe();

        // Send a message directly to the probe
        _fixture.Context.Send(probe, "hello");

        probe.GetNextMessage<string>().Should().Be("hello");
        probe.ExpectNoMessage();
    }
}
