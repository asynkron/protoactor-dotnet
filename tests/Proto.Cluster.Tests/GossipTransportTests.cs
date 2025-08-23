// -----------------------------------------------------------------------
// <copyright file="GossipTransportTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Xunit;
using static Proto.TestKit.TestKit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossipTransportTests
{
    private sealed class MockTransport : IGossipTransport
    {
        private readonly Action<Func<Task<GossipResponse>, Task>, CancellationToken> _handler;

        public MockTransport(Action<Func<Task<GossipResponse>, Task>, CancellationToken> handler) => _handler = handler;

        public void Request(
            IContext context,
            PID pid,
            GossipRequest request,
            Func<Task<GossipResponse>, Task> onResponse,
            CancellationToken cancellationToken) =>
            _handler(onResponse, cancellationToken);
    }

    [Fact]
    public async Task Success_should_commit_offsets()
    {
        var fixture = new InMemoryClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        var cluster = fixture.Members[0];
        var targetMember = fixture.Members[1].MemberList.Self;
        await AwaitConditionAsync(() => cluster.MemberList.ContainsMemberId(targetMember.Id), TimeSpan.FromSeconds(10));
        var committed = false;
        var delta = new MemberStateDelta(targetMember.Id, true, new GossipState(), () => committed = true);
        var request = new GossipRequest { MemberId = cluster.System.Id, State = delta.State };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var transport = new MockTransport((cb, _) =>
        {
            cb(Task.FromResult(new GossipResponse())).ContinueWith(_ => done.SetResult());
        });

        var props = Props.FromFunc(ctx =>
        {
            GossipSender.Send(ctx, cluster.MemberList, targetMember, delta, request, cluster.Config.GossipRequestTimeout, transport);
            return Task.CompletedTask;
        });

        var pid = cluster.System.Root.Spawn(props);
        cluster.System.Root.Send(pid, "go");

        await done.Task;
        Assert.True(committed);
    }

    [Fact]
    public async Task Rejection_should_not_commit_offsets()
    {
        var fixture = new InMemoryClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        var cluster = fixture.Members[0];
        var targetMember = fixture.Members[1].MemberList.Self;
        await AwaitConditionAsync(() => cluster.MemberList.ContainsMemberId(targetMember.Id), TimeSpan.FromSeconds(10));
        var committed = false;
        var delta = new MemberStateDelta(targetMember.Id, true, new GossipState(), () => committed = true);
        var request = new GossipRequest { MemberId = cluster.System.Id, State = delta.State };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var transport = new MockTransport((cb, _) =>
        {
            var resp = new GossipResponse { Rejected = true };
            cb(Task.FromResult(resp)).ContinueWith(_ => done.SetResult());
        });

        var props = Props.FromFunc(ctx =>
        {
            GossipSender.Send(ctx, cluster.MemberList, targetMember, delta, request, cluster.Config.GossipRequestTimeout, transport);
            return Task.CompletedTask;
        });

        var pid = cluster.System.Root.Spawn(props);
        cluster.System.Root.Send(pid, "go");

        await done.Task;
        Assert.False(committed);
    }

    [Fact]
    public async Task Timeout_should_not_commit_offsets()
    {
        var fixture = new InMemoryClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        var cluster = fixture.Members[0];
        var targetMember = fixture.Members[1].MemberList.Self;
        await AwaitConditionAsync(() => cluster.MemberList.ContainsMemberId(targetMember.Id), TimeSpan.FromSeconds(10));
        var committed = false;
        var delta = new MemberStateDelta(targetMember.Id, true, new GossipState(), () => committed = true);
        var request = new GossipRequest { MemberId = cluster.System.Id, State = delta.State };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var transport = new MockTransport((cb, _) =>
        {
            cb(Task.FromException<GossipResponse>(new TimeoutException())).ContinueWith(_ => done.SetResult());
        });

        var props = Props.FromFunc(ctx =>
        {
            GossipSender.Send(ctx, cluster.MemberList, targetMember, delta, request, cluster.Config.GossipRequestTimeout, transport);
            return Task.CompletedTask;
        });

        var pid = cluster.System.Root.Spawn(props);
        cluster.System.Root.Send(pid, "go");

        await done.Task;
        Assert.False(committed);
    }

    [Fact]
    public async Task DeadLetter_should_not_commit_offsets()
    {
        var fixture = new InMemoryClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        var cluster = fixture.Members[0];
        var targetMember = fixture.Members[1].MemberList.Self;
        await AwaitConditionAsync(() => cluster.MemberList.ContainsMemberId(targetMember.Id), TimeSpan.FromSeconds(10));
        var committed = false;
        var delta = new MemberStateDelta(targetMember.Id, true, new GossipState(), () => committed = true);
        var request = new GossipRequest { MemberId = cluster.System.Id, State = delta.State };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var transport = new MockTransport((cb, _) =>
        {
            cb(Task.FromException<GossipResponse>(new DeadLetterException(PID.FromAddress("", "")))).ContinueWith(_ => done.SetResult());
        });

        var props = Props.FromFunc(ctx =>
        {
            GossipSender.Send(ctx, cluster.MemberList, targetMember, delta, request, cluster.Config.GossipRequestTimeout, transport);
            return Task.CompletedTask;
        });

        var pid = cluster.System.Root.Spawn(props);
        cluster.System.Root.Send(pid, "go");

        await done.Task;
        Assert.False(committed);
    }
}
