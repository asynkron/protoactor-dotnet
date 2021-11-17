using Microsoft.Extensions.Logging;
using Proto;

public class ClientActor : IActor
{
    private readonly PID _forwarderOnNodeA;
    private readonly PID _responderOnNodeB;
    private readonly ILogger _logger;

    public ClientActor(PID forwarderOnNodeA, PID responderOnNodeB)
    {
        _forwarderOnNodeA = forwarderOnNodeA.Clone();
        _responderOnNodeB = responderOnNodeB.Clone();
        _logger = Log.CreateLogger<ClientActor>();
    }

    public Task ReceiveAsync(IContext context)
    {
        _logger.LogInformation("Received : {Message}", context.Message);
        switch (context.Message)
        {
            case Started:
                context.Request(_responderOnNodeB, new Ping());
                context.Request(_forwarderOnNodeA, new Ping());
                break;
            default:
                break;
        }
        return Task.CompletedTask;
    }
}
