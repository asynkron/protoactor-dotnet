// -----------------------------------------------------------------------
//  <copyright file="DynamoDBProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Proto.Persistence.DynamoDB
{
    public class DynamoDBProvider : IProvider, IDisposable
    {
        private readonly DynamoDBContext _dynamoDBContext;
        private readonly Table _eventsTable;
        private readonly DynamoDBProviderOptions _options;
        private readonly Table _snapshotsTable;

        public DynamoDBProvider(IAmazonDynamoDB dynamoDBClient, DynamoDBProviderOptions options)
        {
            if (dynamoDBClient == null)
            {
                throw new ArgumentNullException(nameof(dynamoDBClient));
            }

            _options = options ?? throw new ArgumentNullException(nameof(options));

            _dynamoDBContext = new DynamoDBContext(
                dynamoDBClient,
                new DynamoDBContextConfig {Conversion = DynamoDBEntryConversion.V2, ConsistentRead = true}
            );
            _eventsTable = Table.LoadTable(dynamoDBClient, options.EventsTableName, DynamoDBEntryConversion.V2);
            _snapshotsTable = Table.LoadTable(dynamoDBClient, options.SnapshotsTableName, DynamoDBEntryConversion.V2);
        }

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd,
            Action<object> callback)
        {
            QueryOperationConfig config = new QueryOperationConfig {ConsistentRead = true};
            config.Filter.AddCondition(_options.EventsTableHashKey, QueryOperator.Equal, actorName);
            config.Filter.AddCondition(_options.EventsTableSortKey, QueryOperator.Between, indexStart, indexEnd);
            Search query = _eventsTable.Query(config);

            long lastIndex = -1L;

            while (true)
            {
                List<Document> results = await query.GetNextSetAsync();

                foreach (Document doc in results)
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
                DynamoDBEntry dataTypeE = doc.GetValueOrThrow(_options.EventsTableDataTypeKey);
                DynamoDBEntry dataE = doc.GetValueOrThrow(_options.EventsTableDataKey);

                Type dataType = Type.GetType(dataTypeE.AsString());
                return _dynamoDBContext.FromDocumentDynamic(dataE.AsDocument(), dataType);
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            QueryOperationConfig config =
                new QueryOperationConfig {ConsistentRead = true, BackwardSearch = true, Limit = 1};
            config.Filter.AddCondition(_options.SnapshotsTableHashKey, QueryOperator.Equal, actorName);
            Search query = _snapshotsTable.Query(config);
            List<Document> results = await query.GetNextSetAsync();
            Document doc = results.FirstOrDefault();

            if (doc == null)
            {
                return (null, 0);
            }

            DynamoDBEntry snapshotIndexE = doc.GetValueOrThrow(_options.SnapshotsTableSortKey);
            DynamoDBEntry dataTypeE = doc.GetValueOrThrow(_options.SnapshotsTableDataTypeKey);
            DynamoDBEntry dataE = doc.GetValueOrThrow(_options.SnapshotsTableDataKey);

            Type dataType = Type.GetType(dataTypeE.AsString());
            object data = _dynamoDBContext.FromDocumentDynamic(dataE.AsDocument(), dataType);

            return (data, snapshotIndexE.AsLong());
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object eventData)
        {
            Type dataType = eventData.GetType();
            Document data = _dynamoDBContext.ToDocumentDynamic(eventData, dataType);

            Document doc = new Document
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
            Type dataType = snapshotData.GetType();
            Document data = _dynamoDBContext.ToDocumentDynamic(snapshotData, dataType);

            Document doc = new Document
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
            DocumentBatchWrite write = _eventsTable.CreateBatchWrite();
            int writeCount = 0;

            for (int ei = 1; ei <= inclusiveToIndex; ei++)
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
            QueryOperationConfig config = new QueryOperationConfig {ConsistentRead = true};
            config.Filter.AddCondition(_options.SnapshotsTableHashKey, QueryOperator.Equal, actorName);
            config.Filter.AddCondition(_options.SnapshotsTableSortKey, QueryOperator.LessThanOrEqual, inclusiveToIndex);
            Search query = _snapshotsTable.Query(config);

            DocumentBatchWrite write = _snapshotsTable.CreateBatchWrite();
            int writeCount = 0;

            while (true)
            {
                List<Document> results = await query.GetNextSetAsync();

                foreach (Document doc in results)
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
            if (_disposedValue)
            {
                return;
            }

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
