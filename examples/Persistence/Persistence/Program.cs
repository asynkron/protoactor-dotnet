// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Serialization;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Proto;
using Proto.Persistence;
using Proto.Persistence.Couchbase;

class Program
{
    static void Main(string[] args)
    {
        using (var cluster = GetCluster())
        using (var bucket = cluster.OpenBucket("protoactor_test"))
        {
            //NOTE: Don't forget to create index for the bucket!
            //QUERY: CREATE INDEX `persistence` ON `protoactor_test`(type) USING GSI;

            var provider = new CouchbaseProvider(bucket);

            var props = Actor.FromProducer(() => new MyPersistenceActor())
                .WithReceiveMiddleware(Persistence.Using(provider));

            var pid = Actor.Spawn(props);
            
            Console.ReadLine();
        }
    }

    private static ICluster GetCluster()
    {
        var clientDefinition = new CouchbaseClientDefinition
        {
            Buckets = new List<BucketDefinition>
            {
                new BucketDefinition
                {
                    Name = "protoactor_test",
                    ConnectionPool = new ConnectionPoolDefinition
                    {
                        EnableTcpKeepAlives = true,
                        MaxSize = 100,
                        MinSize = 10
                    }
                }
            },
            Servers = new List<Uri>
            {
                new Uri("http://localhost:8091")
            }
        };
        var jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var configuration = new ClientConfiguration(clientDefinition)
        {
            Serializer = () => new DefaultSerializer(jsonSerializerSettings, jsonSerializerSettings)
        };
        return new Cluster(configuration);
    }

    class MyPersistenceActor : IPersistentActor
    {
        private PID _loopActor;
        private State _state = new State();
        public Persistence Persistence { get; set; }
        private class StartLoopActor { }
        private class TimeToSnapshot { }

        private bool _timerStarted = false;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:

                    Console.WriteLine("MyPersistenceActor - Started");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RecoveryStarted msg:

                    Console.WriteLine("MyPersistenceActor - RecoveryStarted");

                    break;
                case RecoveryCompleted msg:

                    Console.WriteLine("MyPersistenceActor - RecoveryCompleted");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RecoverSnapshot msg:
                    
                    if (msg.Data is State ss)
                    {
                        _state = ss;

                        Console.WriteLine("MyPersistenceActor - RecoverSnapshot = {0}, Snapshot.Name = {1}", Persistence.SnapshotIndex, ss.Name);

                    }

                    break;
                case RecoverEvent msg:
                    
                    if (msg.Data is RenameEvent recev)
                    {
                        Console.WriteLine("MyPersistenceActor - RecoverEvent = {0}, Event.Name = {1}", Persistence.EventIndex, recev.Name);
                    }

                    break;
                case PersistedSnapshot msg:

                    await Handle(msg);

                    break;
                case PersistedEvent msg:

                    Console.WriteLine("MyPersistenceActor - PersistedEvent = {0}", msg.Index);

                    if(msg.Data is RenameEvent rne)
                    {
                        _state.Name = rne.Name;
                    }

                    break;
                case RequestSnapshot msg:

                    await Handle(context, msg);

                    break;
                case TimeToSnapshot msg:

                    await Handle(context, msg);

                    break;
                case StartLoopActor msg:

                    await Handle(context, msg);

                    break;
                case RenameCommand msg:

                    await Handle(msg);

                    break;
            }
        }

        private Task Handle(PersistedSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - PersistedSnapshot");

            var sn_index = Persistence.SnapshotIndex - 2;

            //await Persistence.State.DeleteSnapshotsAsync(Persistence.Name, sn_index);

            var ev_index = Persistence.EventIndex;

            //await Persistence.State.DeleteEventsAsync(Persistence.Name, ev_index);

            return Actor.Done;
        }

        private async Task Handle(IContext context, RequestSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - RequestSnapshot");

            await Persistence.PersistSnapshotAsync(_state);

            context.Self.Tell(new TimeToSnapshot());
        }

        private async Task Handle(IContext context, TimeToSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - TimeToSnapshot");

            await Task.Delay(TimeSpan.FromSeconds(10));

            context.Self.Tell(new RequestSnapshot());
        }

        private Task Handle(IContext context, StartLoopActor message)
        {
            if (_timerStarted) return Actor.Done;

            _timerStarted = true;

            Console.WriteLine("MyPersistenceActor - StartLoopActor");

            var props = Actor.FromProducer(() => new LoopActor(Persistence.EventIndex));

            _loopActor = context.Spawn(props);

            context.Self.Tell(new TimeToSnapshot());
            
            return Actor.Done;
        }

        private async Task Handle(RenameCommand message)
        {
            Console.WriteLine("MyPersistenceActor - RenameCommand");

            await Persistence.PersistReceiveAsync(new RenameEvent { Name = message.Name });
        }
    }

    class LoopActor : IActor
    {
        internal class LoopParentMessage { }
        long prefixCounter = 0;

        public LoopActor(long startPrefixCounter)
        {
            prefixCounter = startPrefixCounter;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch(context.Message)
            {
                case Started _:

                    Console.WriteLine("LoopActor - Started");

                    context.Self.Tell(new LoopParentMessage());

                    break;
                case LoopParentMessage msg:

                    Task.Run(async () => {
                        
                        context.Parent.Tell(new RenameCommand { Name = "Daniel-" + prefixCounter });

                        prefixCounter++;

                        await Task.Delay(TimeSpan.FromSeconds(2));

                        context.Self.Tell(new LoopParentMessage());
                    });

                    break;
            }

            return Actor.Done;
        }
    }
}