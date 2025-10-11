using MongoDB.Bson.Serialization.Attributes;

namespace Proto.Cluster.SeedNode.MongoDb;

public class ProtoActorMember
{
    [BsonId]
    private string? Id { get; set; }

    public string MemberId { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = int.MinValue;
}