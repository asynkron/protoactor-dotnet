using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace HostedService
{
    public class ProtoHost : IHostedService
    {
        private Cluster _cluster;
        private ILogger<ProtoHost> _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        public ProtoHost(Cluster cluster, ILogger<ProtoHost> logger, IHostApplicationLifetime appLifetime)
        {
            _cluster = cluster;
            _logger = logger;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cluster...");
            _appLifetime.ApplicationStarted.Register(() => Task.Run(() => RunRequestLoop(), _appLifetime.ApplicationStopping));
            _appLifetime.ApplicationStopping.Register(OnStopping);
            return Task.CompletedTask;

        }

        //flood the system, to see how it reacts upon shutdown.
        private async Task RunRequestLoop()
        {
            await Task.Yield();

            var rnd = new Random();

            while (!_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var id = rnd.Next(0, 100000);
                _ = _cluster.RequestAsync<int>($"abc{id}", "kind", 123, _appLifetime.ApplicationStopping);
            }
        }

        private void OnStopping()
        {
            _logger.LogWarning("Shutting down cluster...");
            var shutdown = _cluster.ShutdownAsync(false);
            var timeout = Task.Delay(15000);
            Task.WhenAny(shutdown, timeout).GetAwaiter().GetResult();
            if (shutdown.IsCompleted)
                _logger.LogWarning("Shut down cluster...");
            else
                _logger.LogError("Shut down cluster timed out...");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}