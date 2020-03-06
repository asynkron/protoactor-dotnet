// -----------------------------------------------------------------------
//  <copyright file="DynamoDBProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Persistence.DynamoDB
{
    public class DynamoDBProviderOptions
    {
        public DynamoDBProviderOptions(string eventsTableName, string snapshotsTableName)
        {
            if (string.IsNullOrEmpty(eventsTableName)) throw new ArgumentNullException(nameof(eventsTableName));
            if (string.IsNullOrEmpty(snapshotsTableName)) throw new ArgumentNullException(nameof(snapshotsTableName));

            EventsTableName = eventsTableName;
            EventsTableHashKey = "ActorName";
            EventsTableSortKey = "EventIndex";
            EventsTableDataKey = "Data";
            EventsTableDataTypeKey = "DataType";

            SnapshotsTableName = snapshotsTableName;
            SnapshotsTableHashKey = "ActorName";
            SnapshotsTableSortKey = "SnapshotIndex";
            SnapshotsTableDataKey = "Data";
            SnapshotsTableDataTypeKey = "DataType";
        }

        public string EventsTableName { get; }
        public string EventsTableHashKey { get; }
        public string EventsTableSortKey { get; }
        public string EventsTableDataKey { get; }
        public string EventsTableDataTypeKey { get; }
        public string SnapshotsTableName { get; }
        public string SnapshotsTableHashKey { get; }
        public string SnapshotsTableSortKey { get; }
        public string SnapshotsTableDataKey { get; }
        public string SnapshotsTableDataTypeKey { get; }
    }
}