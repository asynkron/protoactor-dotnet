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

        public async Task GetEventsAsync(string actorName, ulong eventIndexStart, Action<object> callback)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.eventIndex>={eventIndexStart} AND b.type='event' ORDER BY b.eventIndex ASC";
            var req = QueryRequest.Create(q);
            req.ScanConsistency(ScanConsistency.RequestPlus);
            var res = await _bucket.QueryAsync<Envelope>(req);
            ThrowOnError(res);
            var envelopes = res.Rows;
            foreach (var envelope in envelopes)
            {
                callback(envelope.Event);
            }
        }

        public async Task<Tuple<object, ulong>> GetSnapshotAsync(string actorName)
        {
            var q = $"SELECT b.* FROM `{_bucket.Name}` b WHERE b.actorName={actorName} AND b.type='snapshot' ORDER BY b.eventIndex DESC LIMIT 1";
            var req = QueryRequest.Create(q);
            req.ScanConsistency(ScanConsistency.RequestPlus);
            var res = await _bucket.QueryAsync<Envelope>(req);
            var envelope = res.Rows.FirstOrDefault();
            return envelope != null
                ? Tuple.Create((object) envelope.Event, envelope.EventIndex)
                : null;
        }

        public async Task PersistEventAsync(string actorName, ulong eventIndex, object @event)
        {
            var envelope = new Envelope(actorName, eventIndex, @event, "event");
            var res = await _bucket.InsertAsync(envelope.Key, envelope);
        }

        public async Task PersistSnapshotAsync(string actorName, ulong eventIndex, object snapshot)
        {
            var envelope = new Envelope(actorName, eventIndex, snapshot, "snapshot");
            var res = await _bucket.InsertAsync(envelope.Key, envelope);
        }

        public async Task DeleteEventsAsync(string actorName, ulong fromEventIndex)
        {
            var q = $"SELECT FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.eventIndex<={fromEventIndex} AND b.type='event'";
            var req = QueryRequest.Create(q);
            req.ScanConsistency(ScanConsistency.RequestPlus);
            var res = await _bucket.QueryAsync<Envelope>(req);
            ThrowOnError(res);
            var envelopes = res.Rows;
            foreach (var envelope in envelopes)
            {
                await _bucket.RemoveAsync(envelope.Key);
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, ulong fromEventIndex)
        {
            var q = $"SELECT FROM `{_bucket.Name}` b WHERE b.actorName='{actorName}' AND b.eventIndex<={fromEventIndex} AND b.type='snapshot'";
            var req = QueryRequest.Create(q);
            req.ScanConsistency(ScanConsistency.RequestPlus);
            var res = await _bucket.QueryAsync<Envelope>(req);
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