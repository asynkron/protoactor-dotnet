﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace ProtoActorSut.Shared;

public class ActorSystemHostedService : IHostedService
{
    private readonly ActorSystem _clientSystem;
    private readonly ActorSystem _sutSystem;
    private readonly ILogger<ActorSystemHostedService> _logger;
    

    public ActorSystemHostedService(ProtoActorSUT sut, ProtoActorClient client, ILogger<ActorSystemHostedService> logger)
    {
        _sutSystem = sut.System;
        _clientSystem = client.System;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Proto actor system");
        
        await _sutSystem
            .Cluster()
            .StartMemberAsync();

        await _clientSystem
            .Cluster()
            .StartMemberAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Proto actor system");

        await _sutSystem
            .Cluster()
            .ShutdownAsync();

        await _clientSystem
            .Cluster()
            .ShutdownAsync();
    }
}