using Microsoft.Extensions.Logging;
using Proto;

public class Forwarder : IActor
{
    private readonly PID _target;
    private static ILogger logger = Log.CreateLogger<Forwarder>();

    public Forwarder(PID target)
    {
        _target = target;
    }
    public Task ReceiveAsync(IContext context) => context.Message switch
    {
        Ping => Forward(context),
        object => LogMessage(context),
        _ => Task.CompletedTask
    };

    private Task LogMessage(IContext context)
    {
        logger.LogInformation($"[{context.Self}] -> {context.Message}");
        return Task.CompletedTask;
    }

    private Task Forward(IContext context)
    {
        logger.LogInformation($"{context.Self} Forwarding to {_target}");
        context.Forward(_target);
        return Task.CompletedTask;
    }
}
