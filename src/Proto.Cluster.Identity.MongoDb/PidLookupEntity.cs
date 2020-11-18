namespace Proto.Cluster.Identity.MongoDb
{
    using MongoDB.Bson.Serialization.Attributes;

    public class PidLookupEntity
    {
        [BsonId] public string Key { get; set; } = null!;
        public string Identity { get; set; } = null!;
        public string? UniqueIdentity { get; set; }
        public string Kind { get; set; } = null!;
        public string? Address { get; set; }
        public string? MemberId { get; set; }
        public string? LockedBy { get; set; }
        public int Revision { get; set; }
    }
}