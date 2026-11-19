using System;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Remote.GrpcNet;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests;

[Collection("RemoteTests")]
public class BlockedMemberConnectionTests
{
    private static readonly Ping Ping = new() { Message = "ping" };

    [Fact]
    public async Task BlockedMemberKeepsExistingConnectionsButDoesNotOpenNewOnes()
    {
        await using var blocked = await StartSystemAsync();
        await using var connected = await StartSystemAsync();
        await using var other = await StartSystemAsync();

        var connectedEcho = new PID(connected.Address, "echo");
        (await blocked.Root.RequestAsync<Pong>(connectedEcho, Ping, CancellationTokens.FromSeconds(5))).Message.Should().Be("ping");

        blocked.Remote().BlockList.Block(new[] { blocked.Id }, "test");

        (await blocked.Root.RequestAsync<Pong>(connectedEcho, Ping, CancellationTokens.FromSeconds(5))).Message.Should().Be("ping");
        await Assert.ThrowsAsync<DeadLetterException>(() =>
            blocked.Root.RequestAsync<Pong>(new PID(other.Address, "echo"), Ping, CancellationTokens.FromSeconds(5)));
    }

    [Fact]
    public async Task BlocksFromAMemberWeBlockedAreNotTakenOn()
    {
        await using var system = await StartSystemAsync();
        await using var departed = await StartSystemAsync();

        system.Remote().BlockList.Block(new[] { departed.Id }, "test");
        departed.Remote().BlockList.Block(new[] { "healthy" }, "test");

        // the departed member connects and is refused, sending its block list on the way
        await Assert.ThrowsAnyAsync<Exception>(() =>
            departed.Root.RequestAsync<Pong>(new PID(system.Address, "echo"), Ping, CancellationTokens.FromSeconds(3)));

        system.Remote().BlockList.IsBlocked("healthy").Should().BeFalse();
    }

    private static async Task<ActorSystem> StartSystemAsync()
    {
        var system = new ActorSystem().WithRemote(RemoteConfig.BindToLocalhost().WithProtoMessages(Messages.ProtosReflection.Descriptor));
        await system.Remote().StartAsync();
        system.Root.SpawnNamed(Props.FromFunc(context =>
        {
            if (context.Message is Ping ping)
            {
                context.Respond(new Pong { Message = ping.Message });
            }

            return Task.CompletedTask;
        }), "echo");

        return system;
    }
}
