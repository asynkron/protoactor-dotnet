// -----------------------------------------------------------------------
//  <copyright file="CouchbaseProvider.cs" company="Asynkron HB">
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
    public class CouchbaseProvider : IProvider
    {
        private readonly IBucket _bucket;

        public CouchbaseProvider(IBucket bucket)
        {
            _bucket = bucket;
        }

        public Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            var q = GenerateGetEventsQuery(actorName, indexStart);
            return ExecuteGetEventsQueryAsync(q, callback);
        }

        public Task GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            var q = GenerateGetEventsQuery(actorName, indexStart, indexEnd);
            return ExecuteGetEventsQueryAsync(q, callback);
        }

        private string GenerateGetEventsQuery(string actorName, long indexStart, long? indexEnd = null)
        {
            var s = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'event' AND b.eventIndex >= {indexStart} ";
            if (indexEnd.HasValue)
            {
                s += $"AND b.eventIndex <= {indexEnd.Value} ";
            } 
            s += "ORDER BY b.eventIndex ASC";
            return s;
        }

        private async Task ExecuteGetEventsQueryAsync(string query, Action<object> callback)
        {
            var req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            var events = res.Rows;

            foreach (var @event in events)
            {
                callback(@event.Data);
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'snapshot' ORDER BY b.snapshotIndex DESC LIMIT 1";

            var req = QueryRequest.Create(q);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            var snapshot = res.Rows.FirstOrDefault();
            
            return snapshot != null ? (snapshot.Data, snapshot.SnapshotIndex) : (null, 0);
        }

        public async Task PersistEventAsync(string actorName, long index, object @event)
        {
            var evnt = new Event(actorName, index, @event);

            var res = await _bucket.InsertAsync(evnt.Key, evnt);
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var ss = new Snapshot(actorName, index, snapshot);

            var res = await _bucket.InsertAsync(ss.Key, ss);
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'event' AND b.eventIndex <= {inclusiveToIndex}";

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

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'snapshot' AND b.snapshotIndex <= {inclusiveToIndex}";

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
