// -----------------------------------------------------------------------
//   <copyright file="RemoteHostedService.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Proto.Remote.GrpcNet;

[UsedImplicitly]
[SuppressMessage("", "CA1812")]
internal class RemoteHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IRemote _remote;

    public RemoteHostedService(
        IHostApplicationLifetime appLifetime,
        IRemote remote
    )
    {
        _appLifetime = appLifetime;
        _remote = remote;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStopping.Register(OnStopping);
        _appLifetime.ApplicationStarted.Register(OnStarted);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnStarted() => _remote.StartAsync().GetAwaiter().GetResult();

    private void OnStopping() => _remote.ShutdownAsync().GetAwaiter().GetResult();
}