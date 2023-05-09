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

builder.Services.AddProtoCluster("MyCluster", port: 0,
    configureRemote: r => r.WithProtoMessages(AspNetGrains.Messages.ProtosReflection.Descriptor),
    configureCluster: c =>
        c.WithClusterKind(HelloGrainActor.GetClusterKind((ctx, ci) => new Node2.HelloGrain(ctx, ci.Identity))),
    clusterProvider: SeedNodeClusterProvider.JoinWithDiscovery(discovery));

builder.Services.AddHealthChecks().AddCheck<ClusterHealthCheck>("proto", null, new[] { "ready", "live" });

var app = builder.Build();

app.MapGet("/diagnostics", (ActorSystem system) =>
{
    var entries = system.Diagnostics.GetDiagnostics();
    return entries;
});
app.MapHealthChecks("/health");

app.Run();