// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
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
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Log = Proto.Log;

namespace ClusterExperiment1
{
    public static class Program
    {
        private static ILogger SetupLogger()
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Information,outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
                .MinimumLevel.Information()
                // .Filter.ByExcluding(e => e.Exception != null && e.Level == LogEventLevel.Warning)
                .CreateLogger();
            
            var l = LoggerFactory.Create(l =>
                l.AddSerilog()
                .SetMinimumLevel(LogLevel.Error)
            );
            
            Log.SetLoggerFactory(l);
            var logger = Log.CreateLogger(nameof(Program));
            return logger;
        }

        public static async Task Main()
        {
            var logger = SetupLogger();
            
            var ts = new TaskCompletionSource<bool>();
            
            RunWorkers(ts);

            RunClient(logger);

            await ts.Task;
        }

        private static void RunClient(ILogger logger)
        {
            _ = Task.Run(async () => {
                    await Task.Delay(5000);

                    var cluster = await SpawnClient();
                    var rnd = new Random();

                    while (true)
                    {
                        var id = "myactor" + rnd.Next(0, 1000);

                        try
                        {
                            var res = await cluster.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                                new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token
                            );

                            if (res is null)
                                logger.LogError("Null response");
                            else
                                Console.Write(".");
                        }
                        catch (Exception x)
                        {
                            logger.LogError(x, "Request timeout for {Id}", id);
                        }
                    }
                }
            );
        }

        private static void RunWorkers(TaskCompletionSource<bool> ts)
        {
            var followers = new List<Cluster>();

            for (var i = 0; i < 4; i++)
            {
                var p = SpawnMember();
                followers.Add(p);
            }

            _ = Task.Run(async () => {
                    foreach (var t in followers)
                    {
                        await Task.Delay(20000);
                        Console.WriteLine("Stopping node...");
                        await t.ShutdownAsync(false);
                    }

                    ts.SetResult(true);
                }
            );
        }

        private static async Task<Cluster> SpawnClient()
        {
            var system = new ActorSystem();
            var clusterProvider = ClusterProvider();
            var identity = GetIdentityLookup();
            var (clusterConfig, remoteConfig) = GetClusterConfig(clusterProvider, identity);
            var remote = new GrpcCoreRemote(system, remoteConfig);
            var cluster = new Cluster(system, clusterConfig);
            await cluster.StartClientAsync();
            return cluster;
        }

        private static Cluster SpawnMember()
        {
            var system = new ActorSystem();
            var clusterProvider = ClusterProvider();
            var identity = GetIdentityLookup();
            var helloProps = Props.FromProducer(() => new HelloActor());
            var (clusterConfig, remoteConfig) = GetClusterConfig(clusterProvider, identity);
            clusterConfig = clusterConfig.WithClusterKind("hello", helloProps);
            var remote = new GrpcCoreRemote(system, remoteConfig);
            var cluster = new Cluster(system, clusterConfig);

            cluster.StartMemberAsync();
            return cluster;
        }

        private static (ClusterConfig, GrpcCoreRemoteConfig) GetClusterConfig(IClusterProvider clusterProvider,
            IIdentityLookup identityLookup)
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
                new MongoIdentityStorage("mycluster", db.GetCollection<PidLookupEntity>("pids"))
            );
            return identity;
        }

        private static IMongoDatabase GetMongo()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("MONGO") ?? "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            settings.WriteConcern = WriteConcern.Acknowledged;
            settings.ReadConcern = ReadConcern.Majority;
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }
    }

    public class HelloActor : IActor
    {
        public Task ReceiveAsync(IContext ctx)
        {
            switch (ctx.Message)
            {
                case Started _:
                    //just to highlight when this happens
                    Console.Write("#");
                    break;
                case HelloRequest _: 
                    ctx.Respond(new HelloResponse());
                    break;
            }


            return Task.CompletedTask;
        }
    }
}