using Proto;
using ProtoActorSut.Contracts;

namespace ProtoActorSut.Server;

public class PingPongActor : PingPongActorBase
{
    public PingPongActor(IContext context) : base(context) { }

    public override Task<PongMessage> Ping(PingMessage request) =>
        Task.FromResult(new PongMessage {Response = "Hello " + request.Name});
}