using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Utils;

namespace HostedService
{
    public class ProtoHost : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly Cluster _cluster;
        private readonly ILogger<ProtoHost> _logger;

        public ProtoHost(Cluster cluster, ILogger<ProtoHost> logger, IHostApplicationLifetime appLifetime)
        {
            _cluster = cluster;
            _logger = logger;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cluster...");
            _appLifetime.ApplicationStarted.Register(() => SafeTask.Run(RunRequestLoop, _appLifetime.ApplicationStopping));
            _appLifetime.ApplicationStopping.Register(OnStopping);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        //flood the system, to see how it reacts upon shutdown.
        private async Task RunRequestLoop()
        {
            await Task.Yield();

            var rnd = new Random();

            while (!_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                for (var i = 0; i < 1000; i++)
                {
                    var id = rnd.Next(0, 100000);
                    var t = _cluster.RequestAsync<int>($"abc{id}", "kind", 123, new CancellationTokenSource(20000).Token);
                    tasks.Add(t);
                }

                await Task.WhenAll(tasks);
            }
        }

        private void OnStopping()
        {
            var completedInTime = _cluster.ShutdownAsync()
                .WaitUpTo(TimeSpan.FromSeconds(15))
                .GetAwaiter().GetResult();
            if (!completedInTime)
                _logger.LogError("Shut down cluster timed out...");
        }
    }
}