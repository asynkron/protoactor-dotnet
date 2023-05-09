using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Cluster.Seed;
using StackExchange.Redis;

namespace Proto.Cluster.SeedNode.Redis;

[PublicAPI]
public class RedisSeedNodeDiscovery : ISeedNodeDiscovery
{
    private readonly string _storageKey;
    private readonly IDatabase _db;

    public RedisSeedNodeDiscovery(IConnectionMultiplexer multiplexer, string storageKey = "RedisSeedNode")
    {
        _storageKey = storageKey;
        _db = multiplexer.GetDatabase();
    }
    public async Task Register(string memberId, string host, int port)
    { 
        await _db.HashSetAsync(Key(), memberId, $"{host}:{port}");
    }

    public async Task Remove(string memberId)
    {
        await _db.HashDeleteAsync(Key(), memberId);
    }

    public async Task<(string memberId, string host, int port)[]> GetAll()
    {
        var entries = await _db.HashGetAllAsync(Key());
        var result = entries.Select(x =>
        {
            var parts = x.Value.ToString().Split(':');
            var memberId = x.Name.ToString();
            var host = parts[0];
            var port = int.Parse(parts[1]);
            return (memberId, host, port);
        }).ToArray();

        return result;
    }

    private string Key()
    {
        return _storageKey;
    }
}