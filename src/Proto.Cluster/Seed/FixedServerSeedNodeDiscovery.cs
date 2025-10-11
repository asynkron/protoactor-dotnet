using System.Threading.Tasks;
using Proto.Cluster.SingleNode;

namespace Proto.Cluster.Seed;

public static class FixedServerSeedNode {
    public static IClusterProvider JoinSeedNode(string host, int port)
    {
        return new SeedNodeClusterProvider(
            new SeedNodeClusterProviderOptions(FixedServerSeedNodeDiscovery.JoinSeedNode(host, port)));
    }
    
    public static IClusterProvider StartSeedNode()
    {
        return new SeedNodeClusterProvider(
            new SeedNodeClusterProviderOptions(FixedServerSeedNodeDiscovery.StartSeedNode()));
    }
}

public class FixedServerSeedNodeDiscovery : ISeedNodeDiscovery
{
    private readonly string _host;
    private readonly int _port;
    
    public static ISeedNodeDiscovery JoinSeedNode(string host, int port)
    {
        return new FixedServerSeedNodeDiscovery(host, port);
    }
    
    public static ISeedNodeDiscovery StartSeedNode()
    {
        return new FixedServerSeedNodeDiscovery("", 0);
    }

    private FixedServerSeedNodeDiscovery(string host, int port)
    {
        _host = host;
        _port = port;
    }
    
    public Task Register(string memberId, string host, int port)
    {
        return Task.CompletedTask;
    }

    public Task Remove(string memberId)
    {
        return Task.CompletedTask;
    }

    public Task<(string memberId, string host, int port)[]> GetAll()
    {
        if (_host == "")
        {
            return Task.FromResult(System.Array.Empty<(string memberId, string host, int port)>());
        }
        
        var res = ("SEEDNODE", _host, _port);
        return Task.FromResult(new[] { res });
    }
}