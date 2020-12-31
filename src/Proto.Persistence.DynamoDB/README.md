
## "Hello World"

```csharp
// Initialize Amazon DynamoDBv2 Client.
var client = new AmazonDynamoDBClient("", "", Amazon.RegionEndpoint.USWest1);

// Set options - you can replace table names.
var options = new DynamoDBProviderOptions("events", "snapshots");

// Optionally: Check/Create tables automatically.
// Those 1s at the end are just initial read/write capacities.
// If you don't need snapshots/events don't create that table.
// If not you have to manually create tables!
await DynamoDBHelper.CheckCreateEventsTable(client, options, 1, 1);
await DynamoDBHelper.CheckCreateSnapshotsTable(client, options, 1, 1);

// Initialize provider and use it for your persistent Actors.
var provider = new DynamoDBProvider(_client, options);
```


## Want to manually create tables?  

Events table must have:
* `ActorName` field as hash key
* `EventIndex` field as range key

Snapshots table must have:
* `ActorName` field as hash key
* `SnapshotIndex` field as range key


## Serialization/Deserialization notes

* DateTimeOffset is not supported.
* DateTime precision is down to milliseconds. Milliseconds fractions are lost.