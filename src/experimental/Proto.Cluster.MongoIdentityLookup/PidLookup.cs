using MongoDB.Bson;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class PidLookup
    {
        public ObjectId Id { get; set; }
        public string Key { get; set; }
        public string Identity { get; set; }
        public string Address { get; set; }
    }
}