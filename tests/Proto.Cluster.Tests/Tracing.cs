// -----------------------------------------------------------------------
// <copyright file = "TestTracing.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Proto.Utils;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

public static class Tracing
{
    public const string ActivitySourceName = "Proto.Cluster.Tests";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity StartActivity([CallerMemberName] string callerName = "N/A") =>
        ActivitySource.StartActivity(callerName);

    public static async Task Trace(Func<Task> callBack, ITestOutputHelper testOutputHelper,
        [CallerMemberName] string callerName = "N/A")
    {
        using var activity = StartActivity(callerName);
        
        if (activity is not null)
        {
            activity.AddTag("test.name", callerName);
            testOutputHelper.WriteLine("http://localhost:5001/logs?traceId={0}", activity.TraceId);
        }

        try
        {
            var res = await callBack().WaitUpTo(TimeSpan.FromSeconds(30));
            if (!res)
            {
                testOutputHelper.WriteLine($"{callerName} timedout");
                throw new TimeoutException($"{callerName} timedout");
            }
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.RecordException(e);

            throw;
        }
    }
}