// -----------------------------------------------------------------------
//  <copyright file="CouchbaseProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.N1QL;

namespace Proto.Persistence.Couchbase
{
    internal class CouchbaseProviderState : IProviderState
    {
        private readonly IBucket _bucket;

        public CouchbaseProviderState(IBucket bucket)
        {
            _bucket = bucket;
        }

        public async Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'event' AND b.eventIndex >= {indexStart} ORDER BY b.eventIndex ASC";

            var req = QueryRequest.Create(q);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            var events = res.Rows;

            foreach (var @event in events)
            {
                callback(@event.Data);
            }
        }

        public async Task<Tuple<object, long>> GetSnapshotAsync(string actorName)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'snapshot' ORDER BY b.snapshotIndex DESC LIMIT 1";

            var req = QueryRequest.Create(q);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            var snapshot = res.Rows.FirstOrDefault();

            return snapshot != null ? Tuple.Create((object)snapshot.Data, snapshot.SnapshotIndex) : null;
        }

        public async Task PersistEventAsync(string actorName, long index, object data)
        {
            var @event = new Event(actorName, index, data);

            var res = await _bucket.InsertAsync(@event.Key, @event);
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object data)
        {
            var snapshot = new Snapshot(actorName, index, data);

            var res = await _bucket.InsertAsync(snapshot.Key, snapshot);
        }

        public async Task DeleteEventsAsync(string actorName, long fromIndex)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'event' AND b.eventIndex <= {fromIndex}";

            var req = QueryRequest.Create(q);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            var envelopes = res.Rows;

            foreach (var envelope in envelopes)
            {
                await _bucket.RemoveAsync(envelope.Key);
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long fromIndex)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'snapshot' AND b.snapshotIndex <= {fromIndex}";

            var req = QueryRequest.Create(q);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            var envelopes = res.Rows;

            foreach (var envelope in envelopes)
            {
                await _bucket.RemoveAsync(envelope.Key);
            }
        }

        private static void ThrowOnError<T>(IQueryResult<T> res)
        {
            if (!res.Success)
            {
                throw new Exception($"Couchbase query failed: {res}");
            }
        }
    }
}
