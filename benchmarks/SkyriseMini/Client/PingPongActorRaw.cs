using Proto;
using ProtoActorSut.Contracts;

namespace SkyriseMini;

public class PingPongActorRaw : IActor
{
    private Task Ping(PingMessage request, IContext ctx)
    {
        ctx.Respond(new PongMessage {Response = "Hello " + request.Name});
        return Task.CompletedTask;
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is PingMessage ping)
        {
            return Ping(ping, context);
        }

        return Task.CompletedTask;
    }
}