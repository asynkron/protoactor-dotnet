using Proto;
using Proto.Cluster;
using ProtoActorSut.Contracts;

namespace SkyriseMini;

public class ProtoActorTestServicesRaw
{
    private readonly Cluster _cluster;

    public ProtoActorTestServicesRaw(ActorSystem system)
    {
        _cluster = system.Cluster();
    }

    public async Task Ping(object handle, string name)
    {
        var ci = handle as ClusterIdentity ??
                    throw new ArgumentException($"Handle needs to be of type {nameof(ClusterIdentity)}", nameof(handle));  

        var pong = await _cluster.RequestAsync<PongMessage>(ci, new PingMessage { Name = name}, CancellationTokens.FromSeconds(5));
        
        var expectedResponse = "Hello " + name;

        if (pong == null)
        {
            throw new Exception("Request timed out");
        }
        if (pong.Response != expectedResponse)
            throw new Exception($"Received response '{pong.Response}' but expected '{expectedResponse}'");
    }

    public async Task<object> Activate(string id)
    {
        var ci = ClusterIdentity.Create(id, "PingPongRaw");
        var res = await _cluster.RequestAsync<PongMessage>(ci, new PingMessage(), CancellationToken.None);
        return ci;
    }
}