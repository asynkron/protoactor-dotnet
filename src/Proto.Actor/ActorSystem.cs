﻿// -----------------------------------------------------------------------
// <copyright file="ActorSystem.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;
using Proto.Extensions;
using Proto.Future;
using Proto.Metrics;
using Proto.Utils;

namespace Proto;

/// <summary>
///     Manages all the local actors, their communication and supervision
/// </summary>
[PublicAPI]
public sealed class ActorSystem : IAsyncDisposable
{
    public const string NoHost = "nonhost";
    public const string Client = "$client";
    private readonly ILogger _logger = Log.CreateLogger<ActorSystem>();
    private string _host = NoHost;
    private int _port;

    public ActorSystem() : this(new ActorSystemConfig())
    {
    }

    public ActorSystem(ActorSystemConfig config)
    {
        Stopper = new Stopper();
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Diagnostics = new DiagnosticsStore(this);
        ProcessRegistry = new ProcessRegistry(this);
        Root = NewRoot();
        DeadLetter = new DeadLetterProcess(this).Configure();
        Guardians = new Guardians(this);
        EventStream = new EventStream(this);
        Metrics = new ProtoMetrics(config.MetricsEnabled);
        var eventStream = new EventStreamProcess(this).Configure();
        ProcessRegistry.TryAdd("$eventstream", eventStream);
        Extensions = new ActorSystemExtensions(this);

        DeferredFuture =
            new Lazy<FutureFactory>(() => new FutureFactory(this, config.SharedFutures, config.SharedFutureSize));
        
        Diagnostics.RegisterObject("ActorSystem", "Config", config);
        Diagnostics.RegisterObject("ActorSystem", "Id", Id);
        RunThreadPoolStats();
    }

    /// <summary>
    ///     Unique Id of the actor system. Used as member id for cluster members.
    ///     This is generated by the actor system and is used to identify the actor system in the network.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Address of the actor system when used in Proto.Remote or Proto.Cluster.
    /// </summary>
    public string Address { get; private set; } = NoHost;

    /// <summary>
    ///     Configuration used to create the actor system.
    /// </summary>
    public ActorSystemConfig Config { get; }
    
    /// <summary>
    ///     Diagnostics store, containing detected system issues
    /// </summary>
    public DiagnosticsStore Diagnostics { get; }

    /// <summary>
    ///     Manages all processes in the actor system (actors, futures, event stream, etc.).
    /// </summary>
    public ProcessRegistry ProcessRegistry { get; }

    /// <summary>
    ///     Root context of the actor system. Use it to spawn actors or send messages from outside of an actor context.
    /// </summary>
    public IRootContext Root { get; }

    /// <summary>
    ///     Allows to access the stop cancellation token and stop reason.
    ///     Use <see cref="ShutdownAsync" /> to stop the actor system.
    /// </summary>
    internal Stopper Stopper { get; }

    /// <summary>
    ///     For stopped <see cref="ActorSystem" />, returns the reason for the shutdown.
    /// </summary>
    public string StoppedReason => Stopper.StoppedReason;

    /// <summary>
    ///     Manages all the guardians in the actor system.
    /// </summary>
    public Guardians Guardians { get; }

    /// <summary>
    ///     DeadLetter process that receives all messages that could not be delivered to an actor.
    /// </summary>
    public Process DeadLetter { get; }

    /// <summary>
    ///     Allows to broadcast messages across the actor system to anyone who explicitly subscribed.
    /// </summary>
    public EventStream EventStream { get; }

    /// <summary>
    ///     Diagnostics and metrics for the actor system.
    /// </summary>
    public ProtoMetrics Metrics { get; }

    /// <summary>
    ///     Contains extensions for the actor system. Examples: Cluster, PubSub, etc.
    /// </summary>
    public ActorSystemExtensions Extensions { get; }

    private Lazy<FutureFactory> DeferredFuture { get; }

    internal FutureFactory Future => DeferredFuture.Value;

    /// <summary>
    ///     Cancellation token use to stop the actor system. Register a callback for this token to be notified when the
    ///     ActorSystem begins stopping.
    ///     Use <see cref="ShutdownAsync" /> to stop the actor system.
    /// </summary>
    public CancellationToken Shutdown => Stopper.Token;

    /// <summary>
    ///     Stops the actor system with reason = "Disposed"
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!Shutdown.IsCancellationRequested)
        {
            await ShutdownAsync("Disposed");
        }
    }

    // NOTE: We don't dispose _cts here on purpose, as doing so causes
    // ObjectDisposedException to be thrown from certain background task
    // loops, such as the Gossip loop. Given our usage of the Shutdown token
    // this is not a memory leak.
    //_cts.Dispose();
    private void RunThreadPoolStats()
    {
        var metricTags = new KeyValuePair<string, object?>[] { new("id", Id), new("address", Address) };

        var logger = Log.CreateLogger(nameof(ThreadPoolStats));

        _ = ThreadPoolStats.Run(TimeSpan.FromSeconds(5),
            t =>
            {
                //collect the latency metrics
                if (Metrics.Enabled)
                {
                    ActorMetrics.ThreadPoolLatency.Record(t.TotalSeconds, metricTags);
                }

                //does it take longer than 1 sec for a task to start executing?
                if (t <= Config.ThreadPoolStatsTimeout)
                {
                    return;
                }

                if (Config.DeveloperThreadPoolStatsLogging)
                {
                    Console.WriteLine($"System {Id} - ThreadPool is running hot, ThreadPool latency {t}");
                }

                logger.LogWarning("System {Id} - ThreadPool is running hot, ThreadPool latency {ThreadPoolLatency}", Id,
                    t);
                
            }, Stopper.Token
        );
    }

    /// <summary>
    ///     Stops the actor system and records the reason.
    /// </summary>
    /// <param name="reason">Shutdown reason</param>
    /// <returns></returns>
    public Task ShutdownAsync(string reason = "")
    {
        try
        {
            _logger.LogInformation("Shutting down actor system {Id} - Reason {Reason}", Id, reason);
            Stopper.Stop(reason);
            Diagnostics.RegisterObject("ActorSystem", "Stopped", reason);
        }
        catch
        {
            //pass
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Sets the network address of the actor system. Used by Proto.Remote.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="port"></param>
    public void SetAddress(string host, int port)
    {
        _host = host;
        _port = port;
        Address = $"{host}:{port}";
    }

    /// <summary>
    ///     Sets the address of the actor system to client address. Used by Proto.Remote.
    /// </summary>
    public void SetClientAddress()
    {
        Address = $"{Client}/{Id}";
        _host = Address;
        _port = 0;
    }

    public IRootContext NewRoot(MessageHeader? headers = null, params Func<Sender, Sender>[] middleware)
    {
        var root = new RootContext(this, headers, middleware);
        var configured = Config.ConfigureRootContext(root);
        return configured;
    }

    /// <summary>
    ///     Gets the network address of the actor system. Used by Proto.Remote.
    /// </summary>
    /// <returns></returns>
    public (string Host, int Port) GetAddress() => (_host, _port);

    /// <summary>
    ///     Applies props configuration delegate from actor system configuration.
    /// </summary>
    /// <param name="props"></param>
    /// <returns></returns>
    internal Props ConfigureProps(Props props) => Config.ConfigureProps(props);

    /// <summary>
    ///     Applies props configuration delegate for system actors.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    internal Props ConfigureSystemProps(string name, Props props) => Config.ConfigureSystemProps(name, props);
}
