using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using DynamoDBContext = Amazon.DynamoDBv2.DataModel.DynamoDBContext;
using Proto.Persistence.DynamoDB;

namespace Proto.Persistence.DynamoDB.Tests
{
    [Trait("Category", "Integration")]
    public class IntegrationTests
    {
        public class SomeEvent
        {
            public string Something { get; set; }
            public int Something2 { get; set; }
            public DateTime Date1 { get; set; }
            public DateTime Date2 { get; set; }
        }

        public class SomeSnapshot
        {
            public string Something { get; set; }
            public int Something2 { get; set; }
        }

                public class Nested
        {
            public string IAmNestedString { get; set; }
        }
        public class SomeObj
        {
            public static SomeObj GetMe()
            {
                return new SomeObj
                {
                    IAmString = "This should be some usual string size",
                    IAmInt = Int32.MaxValue,
                    IAmDateTime = DateTime.Now,
                    IAmODateTime2 = DateTime.MaxValue,
                    IAmDecimal = 0.3m,
                    IAmDouble = 0.3,
                    IAmLong = Int64.MaxValue,
                    IAmNested = new Nested { IAmNestedString = "This is another usual string size" }
                };
            }
            public string IAmString { get; set; }
            public int IAmInt { get; set; }
            public DateTime IAmDateTime { get; set; }
            public DateTime IAmODateTime2 { get; set; }
            public decimal IAmDecimal { get; set; }
            public double IAmDouble { get; set; }
            public long IAmLong { get; set; }
            public Nested IAmNested { get; set; }
        }


        private readonly IAmazonDynamoDB _client;
        private readonly Random _random = new Random();

        public IntegrationTests()
        {
            var key = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            _client = new AmazonDynamoDBClient(key, secret, Amazon.RegionEndpoint.USWest1);
        }

        private string GetRandomActorName()
        {
            return "actor_" + Guid.NewGuid().ToString();
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task TableCreationShouldGoThru()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            await DynamoDBHelper.CheckCreateEventsTable(_client, options, 1, 1);
            await DynamoDBHelper.CheckCreateSnapshotsTable(_client, options, 1, 1);
        }


        // *** Events

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task PersistEventShouldGoThru()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistEventAsync(actorName, 1, new SomeEvent {Something = "asdf", Something2 = 1});
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task GetEventsShouldReturnPersistetEventsInOrder()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            var Date1 = DateTime.Now;
            var Date2 = DateTime.Now;
            await provider.PersistEventAsync(actorName, 1, new SomeEvent {Something = "asdf2", Something2 = 2, Date1 = Date1, Date2 = Date2});
            await provider.PersistEventAsync(actorName, 2, new SomeEvent {Something = "asdf3", Something2 = 3});
            
            var retreived = new List<object>();
            await provider.GetEventsAsync(actorName, 1, 2, @event => {
                retreived.Add(@event);
            });
            Assert.Equal(2, retreived.Count);
            var obj1 = retreived.First();
            Assert.IsType<SomeEvent>(obj1);
            var event1 = obj1 as SomeEvent;
            Assert.Equal("asdf2", event1.Something);
            Assert.Equal(2, event1.Something2);
            Assert.Equal(Date1.Truncate(TimeSpan.FromMilliseconds(1)), event1.Date1);
            Assert.Equal(Date2.Truncate(TimeSpan.FromMilliseconds(1)), event1.Date2);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task GetEventsShouldNotCrashIfIndexNotExists()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistEventAsync(actorName, 1, new SomeEvent {Something = "asdf2", Something2 = 2});
            await provider.PersistEventAsync(actorName, 2, new SomeEvent {Something = "asdf3", Something2 = 3});

            var retreived = new List<object>();
            await provider.GetEventsAsync(actorName, 1, 100, @event => {
                retreived.Add(@event);
            });
            Assert.Equal(2, retreived.Count);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task GetEventsShouldReturnJustEventsInRange()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistEventAsync(actorName, 1, new SomeEvent {Something = "asdf2", Something2 = 2});
            await provider.PersistEventAsync(actorName, 2, new SomeEvent {Something = "asdf3", Something2 = 3});
            await provider.PersistEventAsync(actorName, 3, new SomeEvent {Something = "asdf4", Something2 = 4});
            await provider.PersistEventAsync(actorName, 4, new SomeEvent {Something = "asdf5", Something2 = 5});

            var retreived = new List<object>();
            await provider.GetEventsAsync(actorName, 2, 3, @event => {
                retreived.Add(@event);
            });
            Assert.Equal(2, retreived.Count);
            var first = retreived.First() as SomeEvent;
            Assert.NotNull(first);
            Assert.Equal("asdf3", first.Something);
            var second = retreived.Skip(1).First() as SomeEvent;
            Assert.NotNull(second);
            Assert.Equal("asdf4", second.Something);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task DeleteEventsShouldRemoveEvents()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistEventAsync(actorName, 1, new SomeEvent {Something = "asdf2", Something2 = 2});
            await provider.PersistEventAsync(actorName, 2, new SomeEvent {Something = "asdf3", Something2 = 3});
            
            var retreived = new List<object>();
            await provider.GetEventsAsync(actorName, 1, 2, @event => {
                retreived.Add(@event);
            });
            Assert.Equal(2, retreived.Count);
            
            await provider.DeleteEventsAsync(actorName, 1);
            retreived.Clear();
            await provider.GetEventsAsync(actorName, 1, 2, @event => {
                retreived.Add(@event);
            });
            Assert.Equal(1, retreived.Count);
        }


        // *** Snapshots

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task PersistSnapshotShouldGoThru()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistSnapshotAsync(actorName, 1, new SomeSnapshot {Something = "asdf", Something2 = 1});
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task GetSnapshotShouldReturnLastSnapshot()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistSnapshotAsync(actorName, 1, new SomeSnapshot {Something = "asdf2", Something2 = 2});
            await provider.PersistSnapshotAsync(actorName, 2, new SomeSnapshot {Something = "asdf3", Something2 = 3});
            
            var res = await provider.GetSnapshotAsync(actorName);

            Assert.Equal(2, res.Index);
            var snapshot = res.Snapshot as SomeSnapshot;
            Assert.NotNull(snapshot);
            Assert.Equal("asdf3", snapshot.Something);
            Assert.Equal(3, snapshot.Something2);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task GetSnapshotShouldReturnNullIfNoSnapshot()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            
            var res = await provider.GetSnapshotAsync(actorName);

            Assert.Equal(0, res.Index);
            Assert.Null(res.Snapshot);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task DeleteSnapshotsShouldRemoveSnapshots()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistSnapshotAsync(actorName, 1, new SomeSnapshot {Something = "asdf2", Something2 = 2});
            await provider.PersistSnapshotAsync(actorName, 2, new SomeSnapshot {Something = "asdf3", Something2 = 3});
            
            var retreived = new List<object>();
            var res = await provider.GetSnapshotAsync(actorName);
            Assert.Equal(2, res.Index);
            Assert.NotNull(res.Snapshot);
            
            await provider.DeleteSnapshotsAsync(actorName, 1);
            res = await provider.GetSnapshotAsync(actorName);
            Assert.Equal(2, res.Index);
            Assert.NotNull(res.Snapshot);

            await provider.DeleteSnapshotsAsync(actorName, 2);
            res = await provider.GetSnapshotAsync(actorName);
            Assert.Equal(0, res.Index);
            Assert.Null(res.Snapshot);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public async Task DeleteSnapshotsShouldGoThruEventIfIndexToBigger()
        {
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var actorName = GetRandomActorName();
            await provider.PersistSnapshotAsync(actorName, 1, new SomeSnapshot {Something = "asdf2", Something2 = 2});
            await provider.PersistSnapshotAsync(actorName, 2, new SomeSnapshot {Something = "asdf3", Something2 = 3});
            
            var retreived = new List<object>();
            var res = await provider.GetSnapshotAsync(actorName);
            Assert.Equal(2, res.Index);
            Assert.NotNull(res.Snapshot);
            
            await provider.DeleteSnapshotsAsync(actorName, 100);
            res = await provider.GetSnapshotAsync(actorName);
            Assert.Equal(0, res.Index);
            Assert.Null(res.Snapshot);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public void ToDbEntryPerformance()
        {
            var count = 10000;
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var dynamoDBContext = new DynamoDBContext(_client);
            var fromDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "FromDocument" && m.GetParameters().Count() == 1);
            var toDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "ToDocument" && m.GetParameters().Count() == 1);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i<count; i++)
            {
                var obj = SomeObj.GetMe();
                var doc = dynamoDBContext.ToDocumentDynamic(obj, obj.GetType());
            }

            stopwatch.Stop();
            // Console.WriteLine(String.Format("ToDbEntryPerformance: Total: {0}, Per item: {1}", stopwatch.Elapsed.TotalMilliseconds, stopwatch.Elapsed.TotalMilliseconds / count));
            Assert.True(stopwatch.Elapsed.TotalMilliseconds / count < 0.05);
        }

        [Fact(Skip = "Manual DynamoDB integration tests")]
        public void FromDbEntryPerformance()
        {
            var count = 10000;
            var options = new DynamoDBProviderOptions("proto_actor_events", "proto_actor_snapshots");
            var provider = new DynamoDBProvider(_client, options);
            var dynamoDBContext = new DynamoDBContext(_client);
            var fromDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "FromDocument" && m.GetParameters().Count() == 1);
            var toDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "ToDocument" && m.GetParameters().Count() == 1);

            var obj = SomeObj.GetMe();
            var doc = dynamoDBContext.ToDocument(obj);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i<count; i++)
            {
                // Simulate what provider does
                var dataType = Type.GetType(typeof(SomeObj).AssemblyQualifiedName);
                var data = dynamoDBContext.FromDocumentDynamic(doc, dataType);
            }

            stopwatch.Stop();
            // Console.WriteLine(String.Format("FromDbEntryPerformance: Total: {0}, Per item: {1}", stopwatch.Elapsed.TotalMilliseconds, stopwatch.Elapsed.TotalMilliseconds / count));
            Assert.True(stopwatch.Elapsed.TotalMilliseconds / count < 0.05);
        }

    }
}