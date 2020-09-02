// -----------------------------------------------------------------------
//   <copyright file="HostedClusterService.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    public class HostedClusterService : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly Cluster _cluster;

        public HostedClusterService(
            ILogger<HostedClusterService> logger,
            IHostApplicationLifetime appLifetime,
            Cluster cluster)
        {
            _appLifetime = appLifetime;
            _cluster = cluster;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStopping.Register(OnStopping, true);
            _appLifetime.ApplicationStarted.Register(OnStarted, true);
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            _cluster.StartAsync().GetAwaiter().GetResult();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void OnStopping()
        {
            _cluster.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}