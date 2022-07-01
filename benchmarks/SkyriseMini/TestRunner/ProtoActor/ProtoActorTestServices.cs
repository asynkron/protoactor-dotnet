using Proto.Cluster;
using ProtoActorSut.Contracts;

namespace TestRunner.ProtoActor;

public class ProtoActorTestServices
{
    private readonly Cluster _cluster;

    public ProtoActorTestServices(Cluster cluster) => _cluster = cluster;
    
    public async Task Ping(object handle, string name)
    {
        var actor = handle as PingPongActorClient ??
                    throw new ArgumentException($"Handle needs to be of type {nameof(PingPongActorClient)}", nameof(handle));  
        
        var pong = await actor.Ping(new PingMessage { Name = name}, CancellationToken.None);
        
        var expectedResponse = "Hello " + name;

        if (pong?.Response != expectedResponse)
            throw new Exception($"Received response '{pong?.Response}' but expected '{expectedResponse}'");
    }

    public async Task<object> Activate(string id)
    {
        var actor = _cluster.GetPingPongActor(id);
        await actor.Ping(new PingMessage { Name = string.Empty}, CancellationToken.None);
        return actor;
    }
}