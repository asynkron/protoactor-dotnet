﻿// -----------------------------------------------------------------------
// <copyright file="ActivityFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry;
using Xunit;

namespace Proto.OpenTelemetry.Tests;

public sealed class ActivityFixture : IAsyncLifetime
{
    private readonly ConcurrentBag<Activity> _activities = new();
    private readonly ActivityListener _testListener;

    public ActivityFixture()
    {
        Sdk.CreateTracerProviderBuilder(); // static init method will replace No-op trace propagator 

        _testListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Proto.Actor"),
            Sample = SampleAll,
            ActivityStopped = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_testListener);
    }

    public IReadOnlyCollection<Activity> Activities => _activities;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _testListener?.Dispose();

        return Task.CompletedTask;
    }

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> options)
    {
        return ActivitySamplingResult.AllDataAndRecorded;
    }

    public IEnumerable<Activity> GetActivitiesByTraceId(ActivityTraceId traceId)
    {
        return _activities.Where(it => it.TraceId == traceId);
    }
}