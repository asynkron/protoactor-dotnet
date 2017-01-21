using Couchbase.Core;
using Couchbase.N1QL;

namespace Proto.Persistence.Couchbase
{
    public class CouchbaseProvider : IProvider
    {
        private readonly IBucket _bucket;
        public int SnapshotInterval { get; }

        public CouchbaseProvider(IBucket bucket, int snapshotInterval)
        {
            _bucket = bucket;
            SnapshotInterval = snapshotInterval;
        }

        public IProviderState GetState()
        {
            return new CouchbaseProviderState(_bucket, SnapshotInterval);
        }
    }
}
