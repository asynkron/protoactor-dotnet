// -----------------------------------------------------------------------
// <copyright file = "TestTracing.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        await Task.Delay(1).ConfigureAwait(false);
        var logger = Log.CreateLogger(callerName);
        using var activity = StartActivity(callerName);
        logger.LogInformation("Test started");
        var traceId = "";
        var success = true;
        var error = "";
        var sw = Stopwatch.StartNew();

        if (activity is not null)
        {
            traceId = activity.TraceId.ToString();
            activity.AddTag("test.name", callerName);

            var traceViewUrl =
                $"{TracingSettings.TraceViewUrl}/logs?traceId={activity.TraceId.ToString().ToUpperInvariant()}";

            testOutputHelper.WriteLine(traceViewUrl);
            Console.WriteLine($"Running test: {callerName}");
            Console.WriteLine(traceViewUrl);
        }
        else
        {
            testOutputHelper.WriteLine("No active trace span");
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
            error = e.ToString();
            success = false;
            throw;
        }
        finally
        {
            logger.LogInformation("Test ended");

            var f = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (f != null && traceId != "")
            {
                var traceViewUrl =
                    $"{TracingSettings.TraceViewUrl}/logs?traceId={traceId.ToUpperInvariant()}";

                var duration = sw.Elapsed;
                var failIcon =
                    "<img src=\"https://gist.githubusercontent.com/rogeralsing/d8566b01e0850be70f7af9bc9757691e/raw/7ce19c521b44e0e049c3762d8e7afe0314c60175/fail.svg\">";
                var successIcon =
                    "<img src=\"https://gist.githubusercontent.com/rogeralsing/b9165f8eaeb25f05226745c94ab011b6/raw/90b90eb6d762729be6bb88c70e321662b7647ec6/success.svg\">";
                


                var markdown = $@"
{(success ? successIcon : failIcon)} [Test: {callerName}]({traceViewUrl}) - Duration: {duration.TotalMilliseconds} ms <br/>
{(success ? "" : $"Error:\n```\n{error}\n```")}
";
                await File.AppendAllTextAsync(f, markdown);

            }
        }
    }
}