using MongoDB.Driver;
using Proto.Cluster.Seed;

namespace Proto.Cluster.SeedNode.MongoDb;

public class MongoDbSeedNodeDiscovery : ISeedNodeDiscovery
{
    private readonly IMongoCollection<ProtoActorMember> _collection;

    public MongoDbSeedNodeDiscovery(IMongoDatabase mongoDatabase, string storageKey = "ProtoActorMembers")
    {
        _collection = mongoDatabase.GetCollection<ProtoActorMember>(storageKey);
    }

    public async Task Register(string memberId, string host, int port)
    {
        await _collection.InsertOneAsync(new ProtoActorMember
        {
            MemberId = memberId,
            Host = host,
            Port = port
        });
    }

    public async Task Remove(string memberId)
    {
        await _collection.DeleteOneAsync(x => x.MemberId == memberId);
    }

    public async Task<(string memberId, string host, int port)[]> GetAll()
    {
        var mongoResult = await _collection
            .Find(_ => true)
            .ToListAsync();

        var result = mongoResult
            .Select(x => (x.MemberId, x.Host, x.Port))
            .ToArray();

        return result;
    }
}