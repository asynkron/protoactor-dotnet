// -----------------------------------------------------------------------
//  <copyright file="CouchbaseProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.N1QL;

namespace Proto.Persistence.Couchbase
{
    public class CouchbaseProvider : IProvider
    {
        private readonly IBucket _bucket;

        public CouchbaseProvider(IBucket bucket) => _bucket = bucket;

        public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            string query = GenerateGetEventsQuery(actorName, indexStart, indexEnd);

            return ExecuteGetEventsQueryAsync(query, callback);
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            string query =
                $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'snapshot' ORDER BY b.snapshotIndex DESC LIMIT 1";

            IQueryRequest req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            IQueryResult<Snapshot> res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            Snapshot snapshot = res.Rows.FirstOrDefault();

            return snapshot != null ? (snapshot.Data, snapshot.SnapshotIndex) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            Event evnt = new Event(actorName, index, @event);

            await _bucket.InsertAsync(evnt.Key, evnt);

            return index + 1;
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            Snapshot ss = new Snapshot(actorName, index, snapshot);

            return _bucket.InsertAsync(ss.Key, ss);
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            string query =
                $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'event' AND b.eventIndex <= {inclusiveToIndex}";

            IQueryRequest req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            IQueryResult<Event> res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            List<Event> envelopes = res.Rows;

            await Task.WhenAll(envelopes.Select(x => _bucket.RemoveAsync(x.Key)));
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            string query =
                $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'snapshot' AND b.snapshotIndex <= {inclusiveToIndex}";

            IQueryRequest req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            IQueryResult<Snapshot> res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            List<Snapshot> envelopes = res.Rows;

            await Task.WhenAll(envelopes.Select(x => _bucket.RemoveAsync(x.Key)));
        }

        private string GenerateGetEventsQuery(string actorName, long indexStart, long indexEnd)
            => $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' " +
               "AND b.type = 'event' " +
               $"AND b.eventIndex >= {indexStart} " +
               $"AND b.eventIndex <= {indexEnd} " +
               "ORDER BY b.eventIndex ASC";

        private async Task<long> ExecuteGetEventsQueryAsync(string query, Action<object> callback)
        {
            IQueryRequest req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            IQueryResult<Event> res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            List<Event> events = res.Rows;

            foreach (Event @event in events)
            {
                callback(@event.Data);
            }

            return events.LastOrDefault()?.EventIndex ?? -1;
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
