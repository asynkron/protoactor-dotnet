using AspNetGrains.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.Healthchecks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(x => x.AddConsole());

builder.Services.AddProtoCluster("MyCluster",
    configureRemote: r => r.WithProtoMessages(AspNetGrains.Messages.ProtosReflection.Descriptor),
    configureCluster: c => c, clusterProvider:SeedNodeClusterProvider.JoinSeedNode("localhost",8090));

builder.Services.AddHealthChecks().AddCheck<ActorSystemHealthCheck>("proto", null, new[] { "ready", "live" });

var app = builder.Build();

app.MapGet("/", async (Cluster cluster) =>
{
    var helloGrain = cluster.GetHelloGrain("MyGrain");

    var res = await helloGrain.SayHello(new HelloRequest(), CancellationTokens.FromSeconds(5));
    Console.WriteLine(res.Message);

    res = await helloGrain.SayHello(new HelloRequest(), CancellationTokens.FromSeconds(5));
    return res.Message;
});

app.MapGet("/diagnostics", (ActorSystem system) =>
{
    var entries = system.Diagnostics.GetDiagnostics();
    return entries;
});

app.Run();