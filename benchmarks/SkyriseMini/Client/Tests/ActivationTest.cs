using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;


namespace SkyriseMini.Tests;

public class ActivationTest
{
    private readonly Activate _activate;
    private readonly ILogger<ActivationTest> _logger;

    public ActivationTest(Activate activate, ILogger<ActivationTest> logger)
    {
        _activate = activate;
        _logger = logger;
    }

    public async Task RunTest(int activationCount, int parallelism, CancellationToken cancel)
    {
        try
        {
            _logger.LogInformation(
                "Starting activation test with activation count = {ActivationCount}, parallelism = {Parallelism}",
                activationCount, parallelism);

            _logger.LogInformation("Preparing {ActivationCount} actor ids", activationCount);
            var actorIds = await PrepareActorIds(activationCount);
        
            var testDuration = await TestWorker(actorIds, parallelism, cancel);

            _logger.LogInformation(
                "Activation test completed, total activations = {TotalActivations}, duration = {TestDuration}, Throughput = {Throughput:F2} actors/s",
                activationCount, testDuration, activationCount / testDuration.TotalSeconds);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Activation test failed");
        }
    }

    static async Task<ChannelReader<string>> PrepareActorIds(int count)
    {
        var ch = Channel.CreateBounded<string>(count);

        for (int i = 0; i < count; i++)
            await ch.Writer.WriteAsync(Guid.NewGuid().ToString("N"));
        
        ch.Writer.Complete();
        return ch.Reader;
    }
    
    async Task<TimeSpan> TestWorker(ChannelReader<string> actorIds, int parallelism, CancellationToken cancel)
    {
        var overallStopwatch = new Stopwatch();
        overallStopwatch.Start();

        var tasks = Enumerable.Range(1, parallelism).Select(async _ =>
        {
            var activationStopwatch = new Stopwatch();

            await foreach (var actorId in actorIds.ReadAllAsync(cancel))
            {
                try
                {
                    activationStopwatch.Restart();
                    await _activate(actorId);

                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during test");
                }
            }
            
            activationStopwatch.Stop();
        });

        await Task.WhenAll(tasks);

        overallStopwatch.Stop();
        return overallStopwatch.Elapsed;
    }
}