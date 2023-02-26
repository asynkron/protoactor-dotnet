// -----------------------------------------------------------------------
// <copyright file = "TimeoutTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class TimeoutTests
{
    [Fact]
    public async Task ThrowsTimeoutExceptionOnRequestTimeout()
    {
        var fixture = new Fixture(1);
        await using var _ = fixture;
        await fixture.InitializeAsync();

        await fixture.Members.First()
            .Invoking(m => m.RequestAsync<Pong>(CreateIdentity("slow-test"), EchoActor.Kind,
                    new SlowPing { Message = "hi", DelayMs = 4000 }, new CancellationTokenSource(500).Token
                )
            )
            .Should()
            .ThrowAsync<TimeoutException>();
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