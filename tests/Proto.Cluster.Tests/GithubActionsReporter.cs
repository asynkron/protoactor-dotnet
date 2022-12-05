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
    private static readonly ILogger Logger = Log.CreateLogger<GithubActionsReporter>();
    public const string ActivitySourceName = "Proto.Cluster.Tests";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static Activity? StartActivity([CallerMemberName] string callerName = "N/A") =>
        ActivitySource.StartActivity(callerName);
    
    private List<TestResult> _results = new();

    private record TestResult(string Name, string TraceId, TimeSpan Duration, Exception? Exception= null);
    
    private readonly StringBuilder _output = new();

    public async Task Run(Func<Task> test, [CallerMemberName]string testName="")
    {
        await Task.Delay(1).ConfigureAwait(false);
        
        using var activity = StartActivity(testName);
        var traceId=  activity?.Context.TraceId.ToString().ToUpperInvariant() ?? "N/A";
        Logger.LogInformation("Test started");
        Exception? exception = null;
        var sw = Stopwatch.StartNew();
        
        if (activity is not null)
        {
            traceId = activity.TraceId.ToString();
            activity.AddTag("test.name", testName);

            var traceViewUrl =
                $"{TracingSettings.TraceViewUrl}/logs?traceId={traceId}";

            Console.WriteLine($"Running test: {testName}");
            Console.WriteLine(traceViewUrl);
        }

        try
        {
            await test();
            Logger.LogInformation("Test succeeded");
        }
        catch(Exception x)
        {
            exception = x;
            Logger.LogError(x,"Test failed");
        }
        sw.Stop();
        if (activity is not null)
        {
            _results.Add(new TestResult(testName, traceId, sw.Elapsed, exception));
        }
    }

    public async Task WriteReportFile()
    {
        var failIcon =
            "<img src=\"https://gist.githubusercontent.com/rogeralsing/d8566b01e0850be70f7af9bc9757691e/raw/e025b5d58fe3aec1029a5c74f5ab2ee198960fcb/fail.svg\">";
        var successIcon =
            "<img src=\"https://gist.githubusercontent.com/rogeralsing/b9165f8eaeb25f05226745c94ab011b6/raw/cb28ccf1a11c44c8b4c9173bc4aeb98bfa79ca4b/success.svg\">";

        var f = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (f != null)
        {
            _output.AppendLine(@"
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
                _output.AppendLine($@"
<tr>
<td>
{(res.Exception != null ? failIcon : successIcon)}
<a href=""{TracingSettings.TraceViewUrl}/logs?traceId={res.TraceId}"">{res.Name}</a>
</td>
<td>
{res.Duration}
</td>
</tr>");
                if(res.Exception is not null)
                {
                    _output.AppendLine($@"
<tr>
<td colspan=""2"">
<code>
{res.Exception}
</code>
</td>
</tr>");
                }
            }
            _output.AppendLine("</table>");
            
            await File.AppendAllTextAsync(f, _output.ToString());
        }
    }
}