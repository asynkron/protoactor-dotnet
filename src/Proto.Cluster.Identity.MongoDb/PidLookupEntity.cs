using MongoDB.Bson.Serialization.Attributes;

namespace Proto.Cluster.Identity.MongoDb
{
    public class PidLookupEntity
    {
        [BsonId]
        public string Key { get; set; }
        public string Identity { get; set; }
        public string? UniqueIdentity { get; set; }
        public string Kind { get; set; }
        public string? Address { get; set; }
        public string? MemberId { get; set; }
        public string? LockedBy { get; set; }
        public int Revision { get; set; }
    }
}