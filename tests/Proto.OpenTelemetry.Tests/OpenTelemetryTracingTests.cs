using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTelemetry.Trace;
using Proto.Future;
using Xunit;

namespace Proto.OpenTelemetry.Tests;

public class OpenTelemetryTracingTests : IClassFixture<ActivityFixture>
{
    private static readonly Props ProxyTraceActorProps = Props.FromProducer(() => new TraceTestActor()).WithTracing();

    private static readonly Props InnerTraceActorProps = Props.FromFunc(context =>
            {
                if (context.Message is TraceMe)
                {
                    Activity.Current?.SetTag("inner", "true");

                    if (context.Sender is not null)
                    {
                        context.Respond(new TraceResponse());
                    }
                }

                return Task.CompletedTask;
            }
        )
        .WithTracing();

    private static readonly ActivitySource TestSource = new("Proto.Actor.Tests");

    private readonly ActivityFixture _fixture;

    public OpenTelemetryTracingTests(ActivityFixture activityFixture)
    {
        _fixture = activityFixture;
    }

    [Fact]
    public async Task TracesPropagateCorrectlyForSend() =>
        await VerifyTrace(async (rootContext, target) =>
            {
                rootContext.Send(target, new TraceMe(SendAs.Send));
                await Task.Delay(100);
            }
        );

    [Fact]
    public async Task TracesPropagateCorrectlyForRequestAsync() =>
        await VerifyTrace(async (rootContext, target) =>
            {
                var response = await rootContext.RequestAsync<TraceResponse>(target, new TraceMe(SendAs.RequestAsync));
                response.Should().Be(new TraceResponse());
            }
        );

    [Fact]
    public async Task TracesPropagateCorrectlyForRequest() =>
        await VerifyTrace(async (rootContext, target) =>
            {
                rootContext.Request(target, new TraceMe(SendAs.Request));
                await Task.Delay(100);
            }
        );

    [Fact]
    public async Task TracesPropagateCorrectlyForRequestWithForward() =>
        await VerifyTrace(async (rootContext, target) =>
            {
                await rootContext.RequestAsync<TraceResponse>(target, new TraceMe(SendAs.Forward));
            }
        );

    [Fact]
    public async Task TracesPropagateCorrectlyForRequestWithSender() =>
        await VerifyTrace(async (rootContext, target) =>
            {
                var future = new FutureProcess(rootContext.System);
                rootContext.Request(target, new TraceMe(SendAs.Request), future.Pid);
                var response = (MessageEnvelope)await future.Task;
                response.Message.Should().Be(new TraceResponse());
            }
        );

    [Fact]
    public async Task TracesPropagateCorrectlyForRequestWithSenderWithAdditionalMiddleware() =>
        await VerifyTrace(async (tracedRoot, target) =>
            {
                var middleContext = tracedRoot.WithSenderMiddleware(next => async (context, _, envelope) =>
                {
                    var updatedEnvelope = envelope.WithHeader("test", "value");
                    await next(context, target, updatedEnvelope);
                });
                var future = new FutureProcess(middleContext.System);
                middleContext.Request(target, new TraceMe(SendAs.Request), future.Pid);
                var response = (MessageEnvelope)await future.Task;
                response.Message.Should().Be(new TraceResponse());
            }
        );

    /// <summary>
    ///     Checks that we have both the outer and innermost trace present, meaning that the trace has propagated
    ///     across the context boundaries
    /// </summary>
    /// <param name="outerSpanId"></param>
    /// <param name="traceId"></param>
    private void TracesPropagateCorrectly(ActivitySpanId outerSpanId, ActivityTraceId traceId)
    {
        var activities = _fixture.GetActivitiesByTraceId(traceId).OrderBy(it => it.StartTimeUtc).ToList();
        var outerSpan = activities.FirstOrDefault();
        outerSpan.Should().NotBeNull();
        outerSpan!.SpanId.Should().Be(outerSpanId);
        outerSpan.OperationName.Should().Be(nameof(Trace));
        //get second last activity

        var inner = activities.LastOrDefault(s => s.Tags.Contains(new KeyValuePair<string, string?>("inner", "true")));

        inner.Should().NotBeNull();
    }

    private async Task VerifyTrace(Func<IRootContext, PID, Task> action)
    {
        var tracedRoot = new ActorSystem().Root.WithTracing();
        var testRoot = tracedRoot.SpawnNamed(ProxyTraceActorProps, "trace-test");

        var (activitySpanId, activityTraceId) = await Trace(() => action(tracedRoot, testRoot));

        TracesPropagateCorrectly(activitySpanId, activityTraceId);
    }

    private static async Task<(ActivitySpanId, ActivityTraceId)> Trace(Func<Task> action)
    {
        using var activity = TestSource.StartActivity();

        await action();

        return (activity!.SpanId, activity.TraceId);
    }

    [Fact]
    public async Task ExceptionsAreRecorded()
    {
        var actorSystem = new ActorSystem();
        var tracedRoot = actorSystem.Root.WithTracing();
        var testRoot = tracedRoot.SpawnNamed(ProxyTraceActorProps, "trace-test");

        var (_, activityTraceId) = await Trace(async () =>
            {
                tracedRoot.Send(testRoot, new TraceMe(SendAs.Invalid));
                await Task.Delay(100);
            }
        );

        var receiveActivity = _fixture
            .GetActivitiesByTraceId(activityTraceId)
            .Single(it => it.OperationName.Contains("Receive TraceMe", StringComparison.Ordinal));

        receiveActivity.GetStatus().Should().Be(Status.Error);
        receiveActivity.Events.Should().HaveCount(1);
        receiveActivity.Events.Single().Tags.Where(tag => tag.Key.StartsWith("exception")).Should().NotBeEmpty();
    }

    private enum SendAs
    {
        RequestAsync,
        Request,
        Send,
        Forward,
        Invalid
    }

    private record TraceMe(SendAs Method);

    private record TraceResponse;

    public class TraceTestActor : IActor
    {
        private PID? _child;

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is TraceMe msg)
            {
                await OnTraceMe(context, msg);
            }
        }

        private async Task OnTraceMe(IContext context, TraceMe msg)
        {
            var target = GetChild(context);

            switch (msg.Method)
            {
                case SendAs.RequestAsync:
                    ConditionalRespond(context, await context.RequestAsync<object>(target, msg));

                    break;
                case SendAs.Request:
                    var future = new FutureProcess(context.System);
                    context.Request(target, msg, future.Pid);
                    var response = (MessageEnvelope)await future.Task;
                    ConditionalRespond(context, response.Message);

                    break;
                case SendAs.Send:
                    context.Send(target, msg);

                    break;

                case SendAs.Forward:
                    context.Forward(target);

                    break;
                default: throw new ArgumentOutOfRangeException(nameof(msg.Method), msg.Method.ToString());
            }
        }

        private static void ConditionalRespond(IContext context, object remainder)
        {
            if (context.Sender is not null)
            {
                context.Respond(remainder);
            }
        }

        private PID GetChild(IContext context) => _child ??= context.Spawn(InnerTraceActorProps);
    }
}
