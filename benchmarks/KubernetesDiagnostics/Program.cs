using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.Remote;

var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(x => x.AddConsole());

builder.Services.AddProtoCluster((sp, x) =>
{
    x.Port = 0;
    x.ConfigureRemote = r =>
        r.WithAdvertisedHost(advertisedHost);

    x.ConfigureCluster = c => c
        .WithClusterKind("echo", Props.FromFunc(ctx => Task.CompletedTask))
        .WithClusterKind("empty", Props.FromFunc(ctx => Task.CompletedTask));

    x.ClusterProvider = new KubernetesProvider();
    x.IdentityLookup = new PartitionIdentityLookup();
    
});

builder.Services.AddHealthChecks().AddCheck<ClusterHealthCheck>("proto", null, new[] { "ready", "live" });
builder.Services.AddHostedService<DummyHostedService>();

var app = builder.Build();

app.MapGet("/", async (Cluster cluster) =>
{

});

app.MapHealthChecks("/health");

app.Run();



public class DummyHostedService : IHostedService
{
    private readonly ActorSystem _system;
    private readonly ILogger<DummyHostedService> _logger;
    private bool _running;

    public DummyHostedService(ActorSystem system, ILogger<DummyHostedService> logger)
    {
        _system = system;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DummyHostedService");
        _running = true;

        _system.EventStream.Subscribe<ClusterTopology>(e => {

                var hash = e.TopologyHash;
                _logger.LogInformation($"{DateTime.Now:O} My members {hash}");
            }
        );


        var props = Props.FromFunc(ctx => Task.CompletedTask);
        _system.Root.SpawnNamed(props, "dummy");

        var clusterIdentity = ClusterIdentity.Create("some-id", new ClusterKind("echo", Props.FromFunc(ctx => Task.CompletedTask)).Name);

        while (_running)
        {
            var m = _system.Cluster().MemberList.GetAllMembers();
            var hash = Member.TopologyHash(m);
            
            _logger.LogInformation($"{DateTime.Now:O} Hash {hash} Count {m.Length}");

            try
            {
                var t = await _system.Cluster().RequestAsync<Touched>(clusterIdentity, new Touch(), CancellationTokens.FromSeconds(1));

                if (t != null)
                {
                    _logger.LogInformation($"called cluster actor {t.Who}");
                }
                else
                {
                    _logger.LogInformation($"call to cluster actor returned null");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,"Could not call cluster actor");
            }

            foreach (var member in m)
            {
                var pid = new PID(member.Address, "dummy");

                try
                {
                    var t = await _system.Root.RequestAsync<Touched>(pid, new Touch(), CancellationTokens.FromSeconds(1));

                    if (t != null)
                    {
                        _logger.LogInformation("called dummy actor {PID}", pid);
                    }
                    else
                    {
                        _logger.LogInformation("call to dummy actor timed out {PID}", pid);
                    }
                }
                catch
                {
                    _logger.LogInformation("Could not call dummy actor {PID}", pid);
                }
            }

            await Task.Delay(3000);
        }
        
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        return Task.CompletedTask;
    }
}