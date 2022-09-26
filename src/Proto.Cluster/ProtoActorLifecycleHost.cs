using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Proto.Cluster;


public class ProtoActorLifecycleHost : IHostedService
{
    private readonly Cluster _cluster;
    private readonly bool _runAsClient;

    public ProtoActorLifecycleHost(Cluster cluster, bool runAsClient)
    {
        _cluster = cluster;
        _runAsClient = runAsClient;
    }

    public async Task StartAsync(CancellationToken _)
    {
        if (_runAsClient)
        {
            await _cluster.StartClientAsync();
        }
        else
        {
            await _cluster.StartMemberAsync();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cluster.ShutdownAsync();
    }
}