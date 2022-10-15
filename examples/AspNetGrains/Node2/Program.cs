using AspNetGrains.Messages;
using Proto.Cluster;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.Healthchecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(x => x.AddConsole());

builder.Services.AddProtoCluster("MyCluster", port: 8090,
    configureRemote: r => r.WithProtoMessages(AspNetGrains.Messages.ProtosReflection.Descriptor),
    configureCluster: c => c.WithClusterKind(HelloGrainActor.GetClusterKind((ctx,ci) => new Node2.HelloGrain(ctx,ci.Identity))), clusterProvider:SeedNodeClusterProvider.StartSeedNode());

builder.Services.AddHealthChecks().AddCheck<ActorSystemHealthCheck>("proto", null, new[] { "ready", "live" });


var app = builder.Build();

app.Run();