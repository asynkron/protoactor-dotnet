// -----------------------------------------------------------------------
//  <copyright file="CouchbaseProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
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

        public CouchbaseProvider(IBucket bucket) => _bucket = bucket;

        public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            var query = GenerateGetEventsQuery(actorName, indexStart, indexEnd);

            return ExecuteGetEventsQueryAsync(query, callback);
        }

        private string GenerateGetEventsQuery(string actorName, long indexStart, long indexEnd)
            => $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' " +
                "AND b.type = 'event' " +
                $"AND b.eventIndex >= {indexStart} " +
                $"AND b.eventIndex <= {indexEnd} " +
                "ORDER BY b.eventIndex ASC";

        private async Task<long> ExecuteGetEventsQueryAsync(string query, Action<object> callback)
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

            return  events.LastOrDefault()?.EventIndex ?? -1;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            var query =
                $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName = '{actorName}' AND b.type = 'snapshot' ORDER BY b.snapshotIndex DESC LIMIT 1";

            var req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            var snapshot = res.Rows.FirstOrDefault();

            return snapshot != null ? (snapshot.Data, snapshot.SnapshotIndex) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            var evnt = new Event(actorName, index, @event);

            await _bucket.InsertAsync(evnt.Key, evnt);

            return index + 1;
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var ss = new Snapshot(actorName, index, snapshot);

            return _bucket.InsertAsync(ss.Key, ss);
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            var query = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'event' AND b.eventIndex <= {inclusiveToIndex}";

            var req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Event>(req);

            ThrowOnError(res);

            var envelopes = res.Rows;

            await Task.WhenAll(envelopes.Select(x => _bucket.RemoveAsync(x.Key)));
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            var query =
                $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.type = 'snapshot' AND b.snapshotIndex <= {inclusiveToIndex}";

            var req = QueryRequest.Create(query);

            req.ScanConsistency(ScanConsistency.RequestPlus);

            var res = await _bucket.QueryAsync<Snapshot>(req);

            ThrowOnError(res);

            var envelopes = res.Rows;

            await Task.WhenAll(envelopes.Select(x => _bucket.RemoveAsync(x.Key)));
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