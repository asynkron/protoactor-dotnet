// -----------------------------------------------------------------------
// <copyright file="ActivityFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Proto.OpenTelemetry.Tests;

public sealed class ActivityFixture : IAsyncLifetime
{
    private readonly ConcurrentBag<Activity> _activities = new();
    private readonly ActivityListener _testListener;

    public IReadOnlyCollection<Activity> Activities => _activities;

    public ActivityFixture()
    {
        _testListener = new ActivityListener()
        {
            ShouldListenTo = source => source.Name.StartsWith("Proto.Actor"),
            Sample = SampleAll,
            ActivityStopped = activity => _activities.Add(activity)
        };
        ActivitySource.AddActivityListener(_testListener);
    }

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData;

    public IEnumerable<Activity> GetActivitiesByTraceId(ActivityTraceId traceId) => _activities.Where(it => it.TraceId == traceId);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _testListener?.Dispose();
        return Task.CompletedTask;
    }
}