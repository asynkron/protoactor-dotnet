using Proto;

record SomeMessage(PID Pid);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                await context.PoisonAsync(msg.Pid);
                break;
        }
    }
}