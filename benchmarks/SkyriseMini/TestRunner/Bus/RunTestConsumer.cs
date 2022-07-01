using MassTransit;
using TestRunner.Contract;
using TestRunner.Tests;

namespace TestRunner.Bus;

// ReSharper disable once ClassNeverInstantiated.Global
public class RunTestConsumer : IConsumer<RunMessagingTest>, IConsumer<RunActivationTest>
{
    private readonly TestManager _manager;
    private readonly IServiceProvider _provider;

    public RunTestConsumer(TestManager manager, IServiceProvider provider)
    {
        _manager = manager;
        _provider = provider;
    }

    public Task Consume(ConsumeContext<RunMessagingTest> context)
    {
        var test = _provider.GetRequiredService<MessagingTest>();
        _manager.TrackTest(cancel =>
            test.RunTest(context.Message.Parallelism, context.Message.DurationInSeconds, cancel));

        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<RunActivationTest> context)
    {
        var test = _provider.GetRequiredService<ActivationTest>();
        _manager.TrackTest(cancel => 
            test.RunTest(context.Message.ActivationCount, context.Message.Parallelism, cancel));

        return Task.CompletedTask;
    }
}