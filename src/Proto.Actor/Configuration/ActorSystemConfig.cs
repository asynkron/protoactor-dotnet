// -----------------------------------------------------------------------
// <copyright file="ActorSystemConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Context;
using Proto.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable once CheckNamespace
namespace Proto;

[PublicAPI]
public record ActorSystemConfig
{
    /// <summary>
    ///     The interval used to trigger throttling of deadletter message logs
    /// </summary>
    public TimeSpan DeadLetterThrottleInterval { get; init; }

    /// <summary>
    ///     The counter used to trigger throttling of deadletter message logs
    ///     DeadLetter throttling triggers when there are DeadLetterThrottleCount deadletters in DeadLetterThrottleInterval
    ///     time
    /// </summary>
    public int DeadLetterThrottleCount { get; init; }

    /// <summary>
    ///     Enables logging for DeadLetter events in Request/RequestAsync (when a message reaches DeadLetter instead of target
    ///     actor)
    ///     When disabled, the requesting code is responsible for logging manually
    /// </summary>
    public bool DeadLetterRequestLogging { get; set; } = true;

    /// <summary>
    ///     Developer debugging feature, enables extended logging for actor supervision failures
    /// </summary>
    public bool DeveloperSupervisionLogging { get; init; }

    /// <summary>
    ///     Enables actor metrics. Set to true if you want to export the metrics with OpenTelemetry exporters.
    /// </summary>
    public bool MetricsEnabled { get; init; }

    /// <summary>
    ///     Allows adding middleware to the root context exposed by the ActorSystem.
    ///     The result from this will be used as the default sender for all requests,
    ///     except requests overriding the sender context by parameter
    /// </summary>
    [JsonIgnore]
    public Func<RootContext, IRootContext> ConfigureRootContext { get; init; } = context => context;

    /// <summary>
    ///     Allows ActorSystem-wide augmentation of any Props
    ///     All props are translated via this function
    /// </summary>
    [JsonIgnore]
    public Func<Props, Props> ConfigureProps { get; init; } = props => props;

    /// <summary>
    ///     Allows ActorSystem-wide augmentation of system Props
    ///     All system props are translated via this function
    ///     By default, DeadlineDecorator, LoggingContextDecorator are used. Additionally, the supervision strategy is set to
    ///     AlwaysRestart.
    /// </summary>
    [JsonIgnore]
    public Func<string, Props, Props> ConfigureSystemProps { get; init; } = (_, props) =>
    {
        var logger = Log.CreateLogger("Proto.SystemActors");

        return props
            .WithDeadlineDecorator(TimeSpan.FromMilliseconds(100), logger)
            .WithLoggingContextDecorator(logger, LogLevel.None, LogLevel.Debug)
            .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
    };

    /// <summary>
    ///     Enables SharedFutures
    ///     SharedFutures allows the ActorSystem to avoid registering a new temporary process for each request
    ///     Instead registering a SharedFuture that can handle multiple requests internally
    ///     The default is true.
    /// </summary>
    public bool SharedFutures { get; init; } = true;

    /// <summary>
    ///     Sets the number of requests that can be handled by a SharedFuture
    /// </summary>
    public int SharedFutureSize { get; init; } = 5000;

    /// <summary>
    ///     Measures the time it takes from scheduling a Task, until the task starts to execute
    ///     If this deadline expires, the ActorSystem logs that the threadpool is running hot
    /// </summary>
    public TimeSpan ThreadPoolStatsTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Enables more extensive threadpool stats logging
    /// </summary>
    public bool DeveloperThreadPoolStatsLogging { get; init; }

    /// <summary>
    ///     Function used to serialize actor state to a diagnostics string
    ///     Can be used together with RemoteDiagnostics to view the state of remote actors
    /// </summary>
    [JsonIgnore]
    public Func<IActor, string> DiagnosticsSerializer { get; set; } = Diagnostics.DiagnosticsSerializer.Serialize;

    /// <summary>
    ///     The default timeout for RequestAsync calls
    /// </summary>
    public TimeSpan ActorRequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Enables logging for DeadLetter responses in Request/RequestAsync (responses returned from DeadLetter to original
    ///     sender)
    /// </summary>
    public bool DeadLetterResponseLogging { get; init; }

    /// <summary>
    ///     The LogLevel used for diagnostics logging
    /// </summary>
    public LogLevel DiagnosticsLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    ///     Creates a new default ActorSystemConfig
    /// </summary>
    /// <returns>The new ActorSystemConfig</returns>
    public static ActorSystemConfig Setup() => new();

    /// <summary>
    ///     The interval used to trigger throttling of deadletter message logs
    /// </summary>
    public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval) =>
        this with { DeadLetterThrottleInterval = deadLetterThrottleInterval };

    /// <summary>
    ///     The counter used to trigger throttling of deadletter message logs
    ///     DeadLetter throttling triggers when there are DeadLetterThrottleCount deadletters in DeadLetterThrottleInterval
    ///     time
    /// </summary>
    public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount) =>
        this with { DeadLetterThrottleCount = deadLetterThrottleCount };

    /// <summary>
    ///     Enables logging for DeadLetter responses in Request/RequestAsync
    ///     When disabled, the requesting code is responsible for logging manually
    /// </summary>
    public ActorSystemConfig WithDeadLetterRequestLogging(bool enabled) =>
        this with { DeadLetterRequestLogging = enabled };

    /// <summary>
    ///     Enables SharedFutures
    ///     SharedFutures allows the ActorSystem to avoid registering a new temporary process for each request
    ///     Instead registering a SharedFuture that can handle multiple requests internally
    /// </summary>
    /// <param name="size">The number of requests that can be handled by a SharedFuture</param>
    public ActorSystemConfig WithSharedFutures(int size = 5000) =>
        this with { SharedFutures = true, SharedFutureSize = size };

    /// <summary>
    ///     Developer debugging feature, enables extended logging for actor supervision failures
    /// </summary>
    public ActorSystemConfig WithDeveloperSupervisionLogging(bool enabled) =>
        this with { DeveloperSupervisionLogging = enabled };

    /// <summary>
    ///     Enables actor metrics. Set to true if you want to export the metrics with OpenTelemetry exporters.
    /// </summary>
    public ActorSystemConfig WithMetrics(bool enabled = true) => this with { MetricsEnabled = enabled };

    /// <summary>
    ///     Function used to serialize actor state to a diagnostics string
    ///     Can be used together with RemoteDiagnostics to view the state of remote actors
    /// </summary>
    public ActorSystemConfig WithDiagnosticsSerializer(Func<IActor, string> serializer) =>
        this with { DiagnosticsSerializer = serializer };

    /// <summary>
    ///     Allows adding middleware to the root context exposed by the ActorSystem.
    ///     The result from this will be used as the default sender for all requests,
    ///     except requests overriding the sender context by parameter
    /// </summary>
    public ActorSystemConfig WithConfigureRootContext(Func<RootContext, IRootContext> configureContext) =>
        this with { ConfigureRootContext = configureContext };

    /// <summary>
    ///     Allows ActorSystem-wide augmentation of any Props
    ///     All props are translated via this function
    /// </summary>
    public ActorSystemConfig WithConfigureProps(Func<Props, Props> configureProps) =>
        this with { ConfigureProps = configureProps };

    /// <summary>
    ///     Allows ActorSystem-wide augmentation of system Props
    ///     All system props are translated via this function
    ///     By default, DeadlineDecorator, LoggingContextDecorator are used. Additionally, the supervision strategy is set to
    ///     AlwaysRestart.
    /// </summary>
    public ActorSystemConfig WithConfigureSystemProps(Func<string, Props, Props> configureSystemProps) =>
        this with { ConfigureSystemProps = configureSystemProps };

    /// <summary>
    ///     Measures the time it takes from scheduling a Task, until the task starts to execute
    ///     If this deadline expires, the ActorSystem logs that the threadpool is running hot
    /// </summary>
    public ActorSystemConfig WithThreadPoolStatsTimeout(TimeSpan threadPoolStatsTimeout) =>
        this with { ThreadPoolStatsTimeout = threadPoolStatsTimeout };

    /// <summary>
    ///     Enables more extensive threadpool stats logging
    /// </summary>
    public ActorSystemConfig WithDeveloperThreadPoolStatsLogging(bool enabled) =>
        this with { DeveloperThreadPoolStatsLogging = enabled };

    /// <summary>
    ///     The default timeout for RequestAsync calls
    /// </summary>
    public ActorSystemConfig WithActorRequestTimeout(TimeSpan timeout) => this with { ActorRequestTimeout = timeout };

    /// <summary>
    ///     Enables logging for DeadLetter responses in Request/RequestAsync (responses returned from DeadLetter to original
    ///     sender)
    /// </summary>
    public ActorSystemConfig WithDeadLetterResponseLogging(bool enabled) =>
        this with { DeadLetterResponseLogging = enabled };
    
    /// <summary>
    ///     The LogLevel used for Diagnostics logging
    /// </summary>
    /// <param name="diagnosticsLogLevel">The LogLevel used when logging diagnostics</param>
    /// <returns></returns>
    public ActorSystemConfig WithDiagnosticsLogLevel(LogLevel diagnosticsLogLevel) =>
        this with { DiagnosticsLogLevel = diagnosticsLogLevel };


    /// <summary>
    ///     Wraps a given process inside a wrapper process.
    ///     This allows for applying middleware on a process level
    /// </summary>
    [JsonIgnore]
    public Func<Process, Process> ConfigureProcess { get; set; } = process => process;

    /// <summary>
    ///     Wraps a given process inside a wrapper process.
    ///     This allows for applying middleware on a process level
    /// </summary>
    /// <param name="configureProcess">The configure process function</param>
    public ActorSystemConfig WithConfigureProcess(Func<Process, Process> configureProcess) =>
        this with { ConfigureProcess = configureProcess };
}
