// -----------------------------------------------------------------------
//  <copyright file="CouchbaseProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using Couchbase.Core;

namespace Proto.Persistence.Couchbase
{
    public class CouchbaseProvider : IProvider
    {
        private readonly IBucket _bucket;

        public CouchbaseProvider(IBucket bucket, int snapshotInterval)
        {
            _bucket = bucket;
            SnapshotInterval = snapshotInterval;
        }

        public int SnapshotInterval { get; }

        public IProviderState GetState()
        {
            return new CouchbaseProviderState(_bucket, SnapshotInterval);
        }
    }
}