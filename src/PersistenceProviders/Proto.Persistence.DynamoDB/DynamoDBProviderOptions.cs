// -----------------------------------------------------------------------
//  <copyright file="DynamoDBProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Persistence.DynamoDB
{
    public class DynamoDBProviderOptions {
        public DynamoDBProviderOptions(string eventsTableName, string snapshotsTableName)
        {
            if (String.IsNullOrEmpty(eventsTableName)) throw new ArgumentNullException("eventsTableName");
            if (String.IsNullOrEmpty(snapshotsTableName)) throw new ArgumentNullException("snapshotsTableName");

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
        
        public string EventsTableName { get; private set; }
        public string EventsTableHashKey { get; private set; }
        public string EventsTableSortKey { get; private set; }
        public string EventsTableDataKey { get; private set; }
        public string EventsTableDataTypeKey { get; private set; }

        public string SnapshotsTableName { get; private set; }
        public string SnapshotsTableHashKey { get; private set; }
        public string SnapshotsTableSortKey { get; private set; }
        public string SnapshotsTableDataKey { get; private set; }
        public string SnapshotsTableDataTypeKey { get; private set; }
    }
}