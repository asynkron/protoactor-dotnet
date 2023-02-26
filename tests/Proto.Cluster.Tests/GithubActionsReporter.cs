#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Tests;

public class GithubActionsReporter
{
    private readonly string _reportName;
    private static readonly ILogger Logger = Log.CreateLogger<GithubActionsReporter>();
    public const string ActivitySourceName = "Proto.Cluster.Tests";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public GithubActionsReporter(string reportName)
    {
        _reportName = reportName;
    }

    private static Activity? StartActivity([CallerMemberName] string callerName = "N/A") =>
        ActivitySource.StartActivity(callerName);
    
    private readonly List<TestResult> _results = new();

    private record TestResult(string Name, string TraceId, TimeSpan Duration, Exception? Exception= null);
    
    private readonly StringBuilder _output = new();

    public async Task Run(Func<Task> test, [CallerMemberName] string testName = "")
    {
        await Task.Delay(1);

        using var activity = StartActivity(testName);
        var traceId = activity?.Context.TraceId.ToString().ToUpperInvariant() ?? "N/A";
        Logger.LogInformation("Test started");

        var sw = Stopwatch.StartNew();
        try
        {
            if (activity is not null)
            {
                traceId = activity.TraceId.ToString();
                activity.AddTag("test.name", testName);

                var traceViewUrl =
                    $"{TracingSettings.TraceViewUrl}/logs?traceId={traceId}";

                Console.WriteLine($"Running test: {testName}");
                Console.WriteLine(traceViewUrl);
            }
            
            await test();
            Logger.LogInformation("Test succeeded");
            _results.Add(new TestResult(testName, traceId, sw.Elapsed));
        }
        catch (Exception x)
        {
            _results.Add(new TestResult(testName, traceId, sw.Elapsed, x));
            Logger.LogError(x, "Test failed");
            throw;
        }
        finally
        {
            sw.Stop();
        }
    }

    public async Task WriteReportFile()
    {
        var failIcon =
            "<img src=\"https://gist.githubusercontent.com/rogeralsing/d8566b01e0850be70f7af9bc9757691e/raw/e025b5d58fe3aec1029a5c74f5ab2ee198960fcb/fail.svg\">";
        var successIcon =
            "<img src=\"https://gist.githubusercontent.com/rogeralsing/b9165f8eaeb25f05226745c94ab011b6/raw/cb28ccf1a11c44c8b4c9173bc4aeb98bfa79ca4b/success.svg\">";

        var serverUrl = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
        var repositorySlug = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var workspacePath = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        var commitHash = Environment.GetEnvironmentVariable("GITHUB_SHA");
        var f = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (f != null)
        {
            //get some time for traces to propagate
            await Task.Delay(2000);
            _output.AppendLine($@"
<h2>{_reportName}</h2>

<table>
<tr>
<th>
Test
</th>
<th>
Duration
</th>
</tr>");
            
            foreach (var res in _results)
            {
                try
                {
                    _output.AppendLine($@"
<tr>
<td>
{(res.Exception != null ? failIcon : successIcon)}
<a href=""{TracingSettings.TraceViewUrl}/logs?traceId={res.TraceId}"">{res.Name}</a>
</td>
<td>
{res.Duration}
</td>
<td>
   <img height=""50"" src=""{TracingSettings.TraceViewUrl}/api/limit/spanmap/{res.TraceId}/svg"" />
</td>
</tr>");
                    if (res.Exception is not null)
                    {
                        _output.AppendLine($@"
<tr>
<td colspan=""3"">
<code>
{res.Exception.ToString()}
</code>
</td>
</tr>");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            _output.AppendLine("</table>");
            
            await File.AppendAllTextAsync(f, _output.ToString());
        }
    }
}