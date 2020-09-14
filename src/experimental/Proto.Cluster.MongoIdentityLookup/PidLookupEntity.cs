using System;
using MongoDB.Bson;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class PidLookupEntity
    {
        public ObjectId Id { get; set; }
        public string Key { get; set; }
        public string Identity { get; set; }
        public string Kind { get; set; }
        public string Address { get; set; }
        public string MemberId { get; set; }
    }
}