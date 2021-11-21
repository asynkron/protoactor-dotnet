using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTelemetry.Trace;
using Xunit;

namespace Proto.OpenTelemetry.Tests;

public class OpenTelemetryTracingTests : IClassFixture<ActivityFixture>
{
    private static readonly Props TestActorProps = Props.FromProducer(() => new TraceTestActor()).WithTracing();

    private static readonly ActivitySource TestSource = new("Proto.Actor.Tests");

    private readonly ActivityFixture _fixture;

    public OpenTelemetryTracingTests(ActivityFixture activityFixture) => _fixture = activityFixture;

    [Fact]
    public async Task TracesPropagateCorrectly()
    {
        var actorSystem = new ActorSystem();
        var tracedRoot = actorSystem.Root.WithTracing();
        var testRoot = tracedRoot.SpawnNamed(TestActorProps, "trace-test");
        await tracedRoot.RequestAsync<int>(testRoot, 1);

        var (_, activityTraceId) = await Trace(async () => {
                var response = await tracedRoot.RequestAsync<int>(testRoot, 1);
                response.Should().Be(0);
            }
        );

        var activities = _fixture.GetActivitiesByTraceId(activityTraceId).ToList();

        activities.Should().HaveCount(5, "Should include test trace, root request, first actor receive, first actor send, second actor receive");
        var inner = activities.OrderBy(it => it.StartTimeUtc).Last();
        inner.Tags.Should().Contain(new KeyValuePair<string, string?>("remainder", "0"));
    }

    private static async Task<(ActivitySpanId, ActivityTraceId)> Trace(Func<Task> action)
    {
        using var activity = TestSource.StartActivity();

        await action();

        return (activity!.SpanId, activity!.TraceId);
    }

    [Fact]
    public async Task ExceptionsAreRecorded()
    {
        var actorSystem = new ActorSystem();
        var tracedRoot = actorSystem.Root.WithTracing();
        var testRoot = tracedRoot.SpawnNamed(TestActorProps, "trace-test");

        var (_, activityTraceId) = await Trace(async () => {
                tracedRoot.Send(testRoot, "pleaseFail");
                await Task.Delay(100);
            }
        );

        var receiveActivity = _fixture
            .GetActivitiesByTraceId(activityTraceId)
            .Single(it => it.OperationName.Equals("Receive String", StringComparison.Ordinal));

        receiveActivity.GetStatus().Should().Be(Status.Error);
        receiveActivity.Events.Should().HaveCount(1);
        receiveActivity.Events.Single().Tags.Where(tag => tag.Key.StartsWith("exception")).Should().NotBeEmpty();
    }

    public class TraceTestActor : IActor
    {
        private PID? _child;

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is int remainder)
            {
                var activity = Activity.Current;
                activity?.SetTag("remainder", remainder.ToString());
                context.Respond(remainder <= 0 ? remainder : await context.RequestAsync<int>(GetChild(context), remainder - 1));
            }
            else if (context.Message is "pleaseFail")
            {
                throw new Exception("Simulated failure");
            }
        }

        private PID GetChild(IContext context)
            => _child ??= context.Spawn(TestActorProps);
    }
}