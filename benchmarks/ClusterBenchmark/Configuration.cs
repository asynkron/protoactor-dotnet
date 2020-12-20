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
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Serilog;
using Serilog.Events;
using Log = Serilog.Log;

namespace ClusterExperiment1
{
    public static class Configuration
    {
        private static (ClusterConfig, GrpcCoreRemoteConfig) GetClusterConfig(
            IClusterProvider clusterProvider,
            IIdentityLookup identityLookup
        )
        {
            var portStr = Environment.GetEnvironmentVariable("PROTOPORT") ?? $"{RemoteConfigBase.AnyFreePort}";
            var port = int.Parse(portStr);
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? RemoteConfigBase.Localhost;
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            var remoteConfig = GrpcCoreRemoteConfig
                .BindTo(host, port)
                .WithAdvertisedHost(advertisedHost)
                .WithProtoMessages(MessagesReflection.Descriptor);

            var clusterConfig = ClusterConfig
                .Setup("mycluster", clusterProvider, identityLookup);
            return (clusterConfig, remoteConfig);
        }

        private static IClusterProvider ClusterProvider()
        {
            try
            {
                var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
                Console.WriteLine("Running with Kubernetes Provider");
                return new KubernetesProvider(kubernetes);
            }
            catch
            {
                Console.WriteLine("Running with Consul Provider");
                return new ConsulProvider(new ConsulProviderConfig());
            }
        }

        private static IIdentityLookup GetIdentityLookup()
        {
            var db = GetMongo();
            var identity = new IdentityStorageLookup(
                new MongoIdentityStorage("mycluster", db.GetCollection<PidLookupEntity>("pids"),200)
            );
            return identity;
        }

        private static IMongoDatabase GetMongo()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("MONGO") ?? "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            settings.WaitQueueSize = 10000;
            settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            // settings.WriteConcern = WriteConcern.Acknowledged;
            // settings.ReadConcern = ReadConcern.Majority;
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }

        public static async Task<Cluster> SpawnMember()
        {
            var system = new ActorSystem(new ActorSystemConfig().WithDeadLetterThrottleCount(3).WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1)));
            var clusterProvider = ClusterProvider();
            var identity = GetIdentityLookup();
            var helloProps = Props.FromProducer(() => new WorkerActor());
            var (clusterConfig, remoteConfig) = GetClusterConfig(clusterProvider, identity);
            clusterConfig = clusterConfig.WithClusterKind("hello", helloProps);
            var remote = new GrpcCoreRemote(system, remoteConfig);
            var cluster = new Cluster(system, clusterConfig);

            await cluster.StartMemberAsync();
            return cluster;
        }

        public static async Task<Cluster> SpawnClient()
        {
            var system = new ActorSystem(new ActorSystemConfig().WithDeadLetterThrottleCount(3).WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1)));
            var clusterProvider = ClusterProvider();
            var identity = GetIdentityLookup();
            var (clusterConfig, remoteConfig) = GetClusterConfig(clusterProvider, identity);
            var remote = new GrpcCoreRemote(system, remoteConfig);
            var cluster = new Cluster(system, clusterConfig);
            await cluster.StartClientAsync();
            return cluster;
        }

        public static void SetupLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Information, "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
               .CreateLogger();

            var l = LoggerFactory.Create(l =>
                l.AddSerilog()
            );

            Proto.Log.SetLoggerFactory(l);
        }
    }
}