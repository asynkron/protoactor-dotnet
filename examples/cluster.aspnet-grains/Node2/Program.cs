using AspNetGrains.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(x => x.AddConsole());

builder.Services.AddProtoCluster("MyCluster", port: 0,
    configureRemote: r => r.WithProtoMessages(AspNetGrains.Messages.ProtosReflection.Descriptor),
    configureCluster: c =>
        c.WithClusterKind(HelloGrainActor.GetClusterKind((ctx, ci) => new Node2.HelloGrain(ctx, ci.Identity))),
    clusterProvider: new ConsulProvider(new ConsulProviderConfig()));

builder.Services.AddHealthChecks().AddCheck<ClusterHealthCheck>("proto", null, new[] { "ready", "live" });

var app = builder.Build();

app.MapGet("/diagnostics", (ActorSystem system) =>
{
    var entries = system.Diagnostics.GetDiagnostics();
    return entries;
});
app.MapHealthChecks("/health");

app.Run();