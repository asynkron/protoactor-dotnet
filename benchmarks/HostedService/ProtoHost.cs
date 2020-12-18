using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace HostedService
{
    public class ProtoHost : IHostedService
    {
        private Cluster _cluster;
        private ILogger<ProtoHost> _logger;


        public ProtoHost(Cluster cluster, ILogger<ProtoHost> logger)
        {
            _cluster = cluster;
            _logger = logger;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cluster...");
            return Task.CompletedTask;
            
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Shutting down cluster...");
            var shutdown = _cluster.ShutdownAsync(true);
            var timeout = Task.Delay(15000);
            await Task.WhenAny(shutdown, timeout);
            if (shutdown.IsCompleted)
                _logger.LogWarning("Shut down cluster...");
            else
                _logger.LogError("Shut down cluster timed out...");
        }
    }
}