using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SkyriseMini.Monitoring;

namespace SkyriseMini.Tests;

public class MessagingTest
{
    readonly Activate _activate;
    readonly Ping _ping;
    readonly ILogger<MessagingTest> _logger;

    public MessagingTest(Activate activate, Ping ping, ILogger<MessagingTest> logger)
    {
        _activate = activate;
        _ping = ping;
        _logger = logger;
    }

    public async Task RunTest(int parallelism, int durationInSeconds, CancellationToken cancel)
    {
        try
        {
            _logger.LogInformation("Starting messaging test with parallelism = {Parallelism}, duration = {Duration}s", parallelism,
                durationInSeconds);

            var actorIds = PrepareActorIds(parallelism);

            _logger.LogInformation("Activating {Parallelism} actors", parallelism);
            var handles = await ActivateActors(actorIds);

            _logger.LogInformation("Starting the messaging test");
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            cts.CancelAfter(TimeSpan.FromSeconds(durationInSeconds));

            var (totalMessages, testDuration) = await TestWorker(handles, cts.Token);

            _logger.LogInformation("Messaging test completed, total messages = {TotalMessages}, duration = {TestDuration}, Throughput = {Throughput:F2} msg/s",
                totalMessages, testDuration, totalMessages / testDuration.TotalSeconds);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Messaging test failed");
        }
    }

    string[] PrepareActorIds(int count) =>
        Enumerable.Range(1, count).Select(i => $"{Environment.MachineName}-{i}").ToArray();

    async Task<object[]> ActivateActors(string[] actorIds)
    {
        var tasks = actorIds.Select(id => _activate(id)).ToArray();
        await Task.WhenAll(tasks);
        return tasks.Select(t => t.Result).ToArray();
    }

    async Task<(long TotalMessages, TimeSpan TestDuration)> TestWorker(object[] handles, CancellationToken cancel)
    {
        var totalMessages = 0L;
        var overallStopwatch = new Stopwatch();
        overallStopwatch.Start();

        var tasks = handles.Select(async handle =>
        {
            var messageStopwatch = new Stopwatch(); 
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    messageStopwatch.Restart();
                    await _ping(handle, Guid.NewGuid().ToString("N"));
                
                    TestMetrics.MessageLatency.Record(messageStopwatch.ElapsedTicks / (double)Stopwatch.Frequency);
                    TestMetrics.MessageCount.Add(1);
                    Interlocked.Increment(ref totalMessages);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during test");
                    TestMetrics.ErrorCount.Add(1);
                }
            }

            messageStopwatch.Stop();
        });

        await Task.WhenAll(tasks);

        overallStopwatch.Stop();
        return (totalMessages, overallStopwatch.Elapsed);
    }
}