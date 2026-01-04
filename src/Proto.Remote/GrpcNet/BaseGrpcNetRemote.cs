// -----------------------------------------------------------------------
// <copyright file="BaseGrpcNetRemote.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcNet;

/// <summary>
///     Base class for gRPC-based remote implementations that share common shutdown and startup logic.
/// </summary>
public abstract class BaseGrpcNetRemote : IRemote
{
    protected readonly RemoteConfig _config;
    protected readonly object _lock = new();

    protected BaseGrpcNetRemote(ActorSystem system, RemoteConfig config)
    {
        System = system;
        BlockList = new BlockList(system);
        _config = config;
        System.Extensions.Register(this);
        System.Extensions.Register(config.Serialization);
    }

    /// <summary>
    ///     Gets the logger for this remote implementation.
    /// </summary>
    protected abstract ILogger Logger { get; }

    /// <summary>
    ///     Gets the endpoint manager for this remote implementation.
    /// </summary>
    protected abstract EndpointManager EndpointManager { get; }

    public bool Started { get; protected set; }

    public BlockList BlockList { get; }

    public RemoteConfig Config => _config;

    public ActorSystem System { get; }

    public abstract Task StartAsync();

    public virtual async Task ShutdownAsync(bool graceful = true)
    {
        lock (_lock)
        {
            if (!Started)
            {
                return;
            }

            Started = false;
        }

        try
        {
            if (graceful)
            {
                await EndpointManager.StopAsync().ConfigureAwait(false);
            }

            await OnShutdownAsync(graceful).ConfigureAwait(false);

            Logger.LogInformation(
                "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                System.Address, graceful
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex, "Proto.Actor server stopped on {Address} with error: {MessagePayload}",
                System.Address, ex.Message
            );
        }
    }

    /// <summary>
    ///     Called during shutdown after the endpoint manager has stopped.
    ///     Override to perform additional cleanup.
    /// </summary>
    protected virtual Task OnShutdownAsync(bool graceful) => Task.CompletedTask;

    /// <summary>
    ///     Common startup completion logic.
    /// </summary>
    protected void CompleteStartup(string eventName)
    {
        Started = true;
        System.Diagnostics.RegisterEvent("Remote", eventName);
        System.Diagnostics.RegisterObject("Remote", "Config", Config);
        Config.Serialization.Init(System);
    }
}
