// -----------------------------------------------------------------------
//  <copyright file="DynamoDBProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using DynamoDBContext = Amazon.DynamoDBv2.DataModel.DynamoDBContext;
using DynamoDBContextConfig = Amazon.DynamoDBv2.DataModel.DynamoDBContextConfig;

namespace Proto.Persistence.DynamoDB
{
    public class DynamoDBProvider : IProvider, IDisposable
    {
        private readonly DynamoDBProviderOptions _options;
        private readonly DynamoDBContext _dynamoDBContext;
        private readonly Table _eventsTable;
        private readonly Table _snapshotsTable;

        public DynamoDBProvider(IAmazonDynamoDB dynamoDBClient, DynamoDBProviderOptions options)
        {
            if (dynamoDBClient == null) throw new ArgumentNullException(nameof(dynamoDBClient));

            _options = options ?? throw new ArgumentNullException(nameof(options));

            _dynamoDBContext = new DynamoDBContext(
                dynamoDBClient, new DynamoDBContextConfig {Conversion = DynamoDBEntryConversion.V2, ConsistentRead = true}
            );
            _eventsTable = Table.LoadTable(dynamoDBClient, options.EventsTableName, DynamoDBEntryConversion.V2);
            _snapshotsTable = Table.LoadTable(dynamoDBClient, options.SnapshotsTableName, DynamoDBEntryConversion.V2);
        }

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            var config = new QueryOperationConfig {ConsistentRead = true};
            config.Filter.AddCondition(_options.EventsTableHashKey, QueryOperator.Equal, actorName);
            config.Filter.AddCondition(_options.EventsTableSortKey, QueryOperator.Between, indexStart, indexEnd);
            var query = _eventsTable.Query(config);

            var lastIndex = -1L;

            while (true)
            {
                var results = await query.GetNextSetAsync();

                foreach (var doc in results)
                {
                    callback(GetData(doc));
                    lastIndex++;
                }

                if (query.IsDone)
                {
                    break;
                }
            }

            return lastIndex;

            object GetData(Document doc)
            {
                var dataTypeE = doc.GetValueOrThrow(_options.EventsTableDataTypeKey);
                var dataE = doc.GetValueOrThrow(_options.EventsTableDataKey);

                var dataType = Type.GetType(dataTypeE.AsString());
                return _dynamoDBContext.FromDocumentDynamic(dataE.AsDocument(), dataType);
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            var config = new QueryOperationConfig {ConsistentRead = true, BackwardSearch = true, Limit = 1};
            config.Filter.AddCondition(_options.SnapshotsTableHashKey, QueryOperator.Equal, actorName);
            var query = _snapshotsTable.Query(config);
            var results = await query.GetNextSetAsync();
            var doc = results.FirstOrDefault();

            if (doc == null)
            {
                return (null, 0);
            }

            var snapshotIndexE = doc.GetValueOrThrow(_options.SnapshotsTableSortKey);
            var dataTypeE = doc.GetValueOrThrow(_options.SnapshotsTableDataTypeKey);
            var dataE = doc.GetValueOrThrow(_options.SnapshotsTableDataKey);

            var dataType = Type.GetType(dataTypeE.AsString());
            var data = _dynamoDBContext.FromDocumentDynamic(dataE.AsDocument(), dataType);

            return (data, snapshotIndexE.AsLong());
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object eventData)
        {
            var dataType = eventData.GetType();
            var data = _dynamoDBContext.ToDocumentDynamic(eventData, dataType);

            var doc = new Document
            {
                {_options.EventsTableHashKey, actorName},
                {_options.EventsTableSortKey, index},
                {_options.EventsTableDataKey, data},
                {_options.EventsTableDataTypeKey, dataType.AssemblyQualifiedNameSimple()}
            };

            await _eventsTable.PutItemAsync(doc);
            
            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshotData)
        {
            var dataType = snapshotData.GetType();
            var data = _dynamoDBContext.ToDocumentDynamic(snapshotData, dataType);

            var doc = new Document
            {
                {_options.SnapshotsTableHashKey, actorName},
                {_options.SnapshotsTableSortKey, index},
                {_options.SnapshotsTableDataKey, data},
                {_options.SnapshotsTableDataTypeKey, dataType.AssemblyQualifiedNameSimple()}
            };
            await _snapshotsTable.PutItemAsync(doc);
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            // We don't need to query data. Indexes start with one and are sequential.
            var write = _eventsTable.CreateBatchWrite();
            var writeCount = 0;

            for (var ei = 1; ei <= inclusiveToIndex; ei++)
            {
                write.AddKeyToDelete(actorName, ei);

                if (++writeCount >= 25) // 25 is max
                {
                    await write.ExecuteAsync();
                    write = _eventsTable.CreateBatchWrite();
                    writeCount = 0;
                }
            }

            if (writeCount > 0)
            {
                await write.ExecuteAsync();
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            // We do query before deletion because snapshots can be rare (just few indexes).
            var config = new QueryOperationConfig {ConsistentRead = true};
            config.Filter.AddCondition(_options.SnapshotsTableHashKey, QueryOperator.Equal, actorName);
            config.Filter.AddCondition(_options.SnapshotsTableSortKey, QueryOperator.LessThanOrEqual, inclusiveToIndex);
            var query = _snapshotsTable.Query(config);

            var write = _snapshotsTable.CreateBatchWrite();
            var writeCount = 0;

            while (true)
            {
                var results = await query.GetNextSetAsync();

                foreach (var doc in results)
                {
                    write.AddItemToDelete(doc);

                    if (++writeCount >= 25) // 25 is max
                    {
                        await write.ExecuteAsync();
                        write = _snapshotsTable.CreateBatchWrite();
                        writeCount = 0;
                    }
                }

                if (query.IsDone)
                {
                    break;
                }
            }

            if (writeCount > 0)
            {
                await write.ExecuteAsync();
            }
        }

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;

            if (disposing)
            {
                _dynamoDBContext?.Dispose();
            }

            _disposedValue = true;
        }

        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        void IDisposable.Dispose() => Dispose(true);

        #endregion
    }
}