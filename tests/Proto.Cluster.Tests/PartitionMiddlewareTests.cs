using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.PartitionActivator;
using Xunit;

namespace Proto.Cluster.Tests;

public class PartitionActivatorMiddlewareTests : IClassFixture<PartitionActivatorMiddlewareTests.ActivatorMiddlewareFixture>
{
    private readonly ActivatorMiddlewareFixture _fx;

    public PartitionActivatorMiddlewareTests(ActivatorMiddlewareFixture fx) => _fx = fx;

    [Fact]
    public async Task ShouldInvokeActivatorMiddleware()
    {
        var cluster = _fx.Members[0];
        var identity = ClusterIdentity.Create("act", EchoActor.Kind);
        await cluster.RequestAsync<Pong>(identity, new Ping(), CancellationToken.None);

        _fx.ActivationRequests.Should().BeGreaterThan(0);
    }

    public class ActivatorMiddlewareFixture : BaseInMemoryClusterFixture
    {
        public int ActivationRequests;

        public ActivatorMiddlewareFixture() : base(1, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName) =>
            new PartitionActivatorLookup(props => props.WithReceiverMiddleware(next => async (ctx, env) =>
            {
                if (env.Message is ActivationRequest)
                {
                    Interlocked.Increment(ref ActivationRequests);
                }

                await next(ctx, env);
            }));
    }
}

public class PartitionPlacementMiddlewareTests : IClassFixture<PartitionPlacementMiddlewareTests.PlacementMiddlewareFixture>
{
    private readonly PlacementMiddlewareFixture _fx;

    public PartitionPlacementMiddlewareTests(PlacementMiddlewareFixture fx) => _fx = fx;

    [Fact]
    public async Task ShouldInvokePlacementMiddleware()
    {
        var cluster = _fx.Members[0];
        var identity = ClusterIdentity.Create("place", EchoActor.Kind);
        await cluster.RequestAsync<Pong>(identity, new Ping(), CancellationToken.None);

        _fx.ActivationRequests.Should().BeGreaterThan(0);
    }

    public class PlacementMiddlewareFixture : BaseInMemoryClusterFixture
    {
        public int ActivationRequests;

        public PlacementMiddlewareFixture() : base(1, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName) =>
            new PartitionIdentityLookup(new PartitionConfig(), props => props.WithReceiverMiddleware(next => async (ctx, env) =>
            {
                if (env.Message is ActivationRequest)
                {
                    Interlocked.Increment(ref ActivationRequests);
                }

                await next(ctx, env);
            }));
    }
}
