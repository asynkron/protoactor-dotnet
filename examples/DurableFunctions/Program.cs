using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Durable;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Remote.GrpcCore;
using Serilog;
using Serilog.Events;
using Log = Serilog.Log;

namespace DurableFunctions
{
    class Program
    {
        static async Task Main(string[] args)
        {
            SetupLogger();

            var db = GetMongo();
            var pids = db.GetCollection<PidLookupEntity>("pids");

            var identity = new IdentityStorageLookup(new MongoIdentityStorage("mycluster", pids));
            var provider = new ConsulProvider(new ConsulProviderConfig());
            var system = new ActorSystem()
                .WithRemote(
                    GrpcCoreRemoteConfig
                        .BindToLocalhost()
                )
                .WithCluster(
                    ClusterConfig
                        .Setup("mycluster",provider,identity)
                        .WithClusterKind("MyFunc", Props.FromProducer(() => new MyFunction()))
                        .WithClusterKind("SomeActor",Props.FromProducer(() => new SomeActor()))
                )
                .WithDurableFunctions();

            await system
                .Cluster()
                .StartMemberAsync();

            await system.Cluster().RequestAsync<int>("foo", "MyFunc", 123, CancellationToken.None);

            Console.ReadLine();
        }

        private static void SetupLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Information, "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
                .CreateLogger();

            var l = LoggerFactory.Create(l =>
                l.AddSerilog()
            );

            Proto.Log.SetLoggerFactory(l);
        }

        private static IMongoDatabase GetMongo()
        {
            var connectionString = "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            var client = new MongoClient(settings);
            var database = client.GetDatabase("DurableFunctions");
            return database;
        }
    }

    public class MyFunction : DurableFunction
    {
        protected override async Task Run(DurableContext context)
        {
            var x = await context.RequestAsync<int>("foo", "SomeActor", 222);
            var y = await context.RequestAsync<int>("foo", "SomeActor", 333);
            Console.WriteLine($"result {x * y}");
        }
    }

    public class SomeActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is int i)
            {
                Console.WriteLine($"got call for {i}");
                context.Respond(i*2);
            }

            return Task.CompletedTask;
        }
    }
}