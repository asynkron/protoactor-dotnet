using MongoDB.Bson.Serialization.Attributes;
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Proto.Cluster.Identity.MongoDb
{
    [JetBrains.Annotations.UsedImplicitly]
    public class PidLookupEntity
    {
        [BsonId]
        public string Key { get; set; } = default!;
        public string Identity { get; set; } = default!;
        public string? UniqueIdentity { get; set; } = default!;
        public string Kind { get; set; } = default!;
        public string? Address { get; set; }
        public string? MemberId { get; set; }
        public string? LockedBy { get; set; }
        public int Revision { get; set; }
    }
}