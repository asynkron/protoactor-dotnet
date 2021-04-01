// -----------------------------------------------------------------------
// <copyright file="Configuration.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using k8s;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using Log = Serilog.Log;

namespace ClusterExperiment1
{
    public static class Configuration
    {
        private static ClusterConfig GetClusterConfig(
            IClusterProvider clusterProvider,
            IIdentityLookup identityLookup
        )
        {
            Props helloProps = Props.FromProducer(() => new WorkerActor());
            return ClusterConfig
                .Setup("mycluster", clusterProvider, identityLookup)
                .WithClusterKind("hello", helloProps);
        }

        private static GrpcCoreRemoteConfig GetRemoteConfig()
        {
            string? portStr = Environment.GetEnvironmentVariable("PROTOPORT") ?? $"{RemoteConfigBase.AnyFreePort}";
            int port = int.Parse(portStr);
            string? host = Environment.GetEnvironmentVariable("PROTOHOST") ?? RemoteConfigBase.Localhost;
            string? advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            GrpcCoreRemoteConfig remoteConfig = GrpcCoreRemoteConfig
                .BindTo(host, port)
                .WithAdvertisedHost(advertisedHost)
                .WithProtoMessages(MessagesReflection.Descriptor)
                .WithEndpointWriterMaxRetries(2);

            return remoteConfig;
        }

        private static IClusterProvider ClusterProvider()
        {
            try
            {
                Kubernetes kubernetes = new(KubernetesClientConfiguration.InClusterConfig());
                Console.WriteLine("Running with Kubernetes Provider");
                return new KubernetesProvider(kubernetes);
            }
            catch
            {
                Console.WriteLine("Running with Consul Provider");
                return new ConsulProvider(new ConsulProviderConfig());
            }
        }

        public static IIdentityLookup GetIdentityLookup() =>
            GetMongoIdentityLookup(); //  GetRedisIdentityLookup();// new PartitionIdentityLookup(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));

        private static IIdentityLookup GetRedisIdentityLookup()
        {
            ConnectionMultiplexer multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
            RedisIdentityStorage redisIdentityStorage =
                new("mycluster", multiplexer, maxConcurrency: 50);

            return new IdentityStorageLookup(redisIdentityStorage);
        }

        private static IIdentityLookup GetMongoIdentityLookup()
        {
            IMongoDatabase db = GetMongo();
            IdentityStorageLookup identity = new(
                new MongoIdentityStorage("mycluster", db.GetCollection<PidLookupEntity>("pids"), 200)
            );
            return identity;
        }

        private static IMongoDatabase GetMongo()
        {
            string? connectionString =
                Environment.GetEnvironmentVariable("MONGO") ?? "mongodb://127.0.0.1:27017/ProtoMongo";
            MongoUrl url = MongoUrl.Create(connectionString);
            MongoClientSettings settings = MongoClientSettings.FromUrl(url);
            // settings.WaitQueueSize = 10000;
            // settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            //
            // settings.WriteConcern = WriteConcern.WMajority;
            // settings.ReadConcern = ReadConcern.Majority;
            MongoClient client = new(settings);
            IMongoDatabase database = client.GetDatabase("ProtoMongo");
            return database;
        }

        public static async Task<Cluster> SpawnMember()
        {
            ActorSystem system = new(new ActorSystemConfig().WithDeadLetterThrottleCount(3)
                .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1))
                .WithDeadLetterRequestLogging(false)
            );
            system.EventStream.Subscribe<ClusterTopology>(e =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{system.Id}-ClusterTopology:{e.GetMembershipHashCode()}");
                Console.ResetColor();
            });
            system.EventStream.Subscribe<LeaderElected>(e =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{system.Id}-Leader:{e.Leader.Id}");
                Console.ResetColor();
            });
            IClusterProvider clusterProvider = ClusterProvider();
            IIdentityLookup identity = GetIdentityLookup();

            system.WithRemote(GetRemoteConfig()).WithCluster(GetClusterConfig(clusterProvider, identity));
            await system.Cluster().StartMemberAsync();
            return system.Cluster();
        }

        public static async Task<Cluster> SpawnClient()
        {
            ActorSystem system = new(new ActorSystemConfig().WithDeadLetterThrottleCount(3)
                .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1))
                .WithDeadLetterRequestLogging(false)
            );
            system.EventStream.Subscribe<ClusterTopology>(e =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{system.Id}-ClusterTopology:{e.GetMembershipHashCode()}");
                Console.ResetColor();
            });
            system.EventStream.Subscribe<LeaderElected>(e =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{system.Id}-Leader:{e.Leader.Id}");
                Console.ResetColor();
            });
            IClusterProvider clusterProvider = ClusterProvider();
            IIdentityLookup identity = GetIdentityLookup();
            system.WithRemote(GetRemoteConfig()).WithCluster(GetClusterConfig(clusterProvider, identity));

            await system.Cluster().StartClientAsync();
            return system.Cluster();
        }

        public static void SetupLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Error)
                .CreateLogger();

            Proto.Log.SetLoggerFactory(LoggerFactory.Create(l =>
                    l.AddSerilog().SetMinimumLevel(LogLevel.Error)
                )
            );
        }
    }
}
