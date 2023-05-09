using AspNetGrains.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Seed;
using Proto.Cluster.SeedNode.Redis;
using Proto.Remote;
using Proto.Remote.HealthChecks;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(x => x.AddConsole());

var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
var discovery = new RedisSeedNodeDiscovery(multiplexer);

builder.Services.AddProtoCluster("MyCluster", port: 8090,
    configureRemote: r => r.WithProtoMessages(AspNetGrains.Messages.ProtosReflection.Descriptor),
    configureCluster: c => c, clusterProvider: SeedNodeClusterProvider.JoinWithDiscovery(discovery));

builder.Services.AddHealthChecks().AddCheck<ClusterHealthCheck>("proto", null, new[] { "ready", "live" });

var app = builder.Build();

app.MapGet("/", async (Cluster cluster) =>
{
    var helloGrain = cluster.GetHelloGrain("MyGrain");

    var res = await helloGrain.SayHello(new HelloRequest(), CancellationTokens.FromSeconds(5));
    Console.WriteLine(res.Message);

    return res.Message;
});

app.MapGet("/diagnostics", (ActorSystem system) =>
{
    var entries = system.Diagnostics.GetDiagnostics();
    return entries;
});

app.MapHealthChecks("/health");

app.Run();