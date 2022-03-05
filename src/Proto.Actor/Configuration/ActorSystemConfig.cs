// -----------------------------------------------------------------------
// <copyright file="ActorSystemConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto;

[PublicAPI]
public record ActorSystemConfig
{
    /// <summary>
    /// The interval used to trigger DeadLetter throttling
    /// </summary>
    public TimeSpan DeadLetterThrottleInterval { get; init; }
    
    /// <summary>
    /// The counter used to trigger DeadLetter throttling
    /// DeadLetter throttling triggers when there are DeadLetterThrottleCount deadletters in DeadLetterThrottleInterval time
    /// </summary>
    public int DeadLetterThrottleCount { get; init; }

    /// <summary>
    /// Enables logging for DeadLetter responses in Request/RequestAsync
    /// When disabled, the requesting code is responsible for logging manually
    /// </summary>
    public bool DeadLetterRequestLogging { get; set; } = true;
    
    /// <summary>
    /// Developer debugging feature, enables extended logging for actor supervision failures
    /// </summary>
    public bool DeveloperSupervisionLogging { get; init; }
    
    /// <summary>
    /// Enables actor metrics
    /// </summary>
    public bool MetricsEnabled { get; init; }

    /// <summary>
    /// Allows ActorSystem-wide augmentation of any Props
    /// All props are translated via this function
    /// </summary>
    public Func<Props, Props> ConfigureProps { get; init; } = props => props;

    /// <summary>
    /// Enables SharedFutures
    /// SharedFutures allows the ActorSystem to avoid registering a new temporary process for each request
    /// Instead registering a SharedFuture that can handle multiple requests internally
    /// </summary>
    public bool SharedFutures { get; init; }
    
    /// <summary>
    /// Sets the number of requests that can be handled by a SharedFuture
    /// </summary>
    public int SharedFutureSize { get; init; } = 5000;

    /// <summary>
    /// Measures the time it takes from scheduling a Task, until the task starts to execute
    /// If this deadline expires, the ActorSystem logs that the threadpool is running hot
    /// </summary>
    public TimeSpan ThreadPoolStatsTimeout { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Enables more extensive threadpool stats logging
    /// </summary>
    public bool DeveloperThreadPoolStatsLogging { get; init; }

    /// <summary>
    /// Creates a new default ActorSystemConfig
    /// </summary>
    /// <returns>The new ActorSystemConfig</returns>
    public static ActorSystemConfig Setup() => new();

    /// <summary>
    /// Function used to serialize actor state to a diagnostics string
    /// Can be used together with RemoteDiagnostics to view the state of remote actors
    /// </summary>
    public Func<IActor, string> DiagnosticsSerializer { get; set; } = Diagnostics.DiagnosticsSerializer.Serialize;
    
    /// <summary>
    /// The default timeout for RequestAsync calls 
    /// </summary>
    public TimeSpan RequestAsyncTimeout { get; init; } = TimeSpan.FromSeconds(5);

    
    public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval) =>
        this with {DeadLetterThrottleInterval = deadLetterThrottleInterval};

    public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount) =>
        this with {DeadLetterThrottleCount = deadLetterThrottleCount};

    public ActorSystemConfig WithDeadLetterRequestLogging(bool enabled) => this with {DeadLetterRequestLogging = enabled};

    public ActorSystemConfig WithSharedFutures(int size = 5000) => this with {SharedFutures = true, SharedFutureSize = size};

    public ActorSystemConfig WithDeveloperSupervisionLogging(bool enabled) => this with {DeveloperSupervisionLogging = enabled};

    public ActorSystemConfig WithMetrics(bool enabled = true) => this with {MetricsEnabled = enabled};

    public ActorSystemConfig WithDiagnosticsSerializer(Func<IActor, string> serializer) => this with {DiagnosticsSerializer = serializer};
        
    public ActorSystemConfig WithConfigureProps(Func<Props, Props> configureProps) => this with {ConfigureProps = configureProps};
        
    public ActorSystemConfig WithThreadPoolStatsTimeout(TimeSpan threadPoolStatsTimeout) => this with {ThreadPoolStatsTimeout = threadPoolStatsTimeout};
        
    public ActorSystemConfig WithDeveloperThreadPoolStatsLogging(bool enabled) => this with {DeveloperThreadPoolStatsLogging = enabled};
        
}

//Not part of the contract, but still shipped out of the box
public static class ActorSystemConfigExtensions
{
    public static ActorSystemConfig WithDeveloperReceiveLogging(this ActorSystemConfig self, TimeSpan receiveDeadline, LogLevel logLevel= LogLevel.Error)
    {
        var inner = self.ConfigureProps;
        var logger = Log.CreateLogger("DeveloperReceive");
            
        Receiver DeveloperReceiveLogging(Receiver next) => (context, envelope) => {
            var sw = Stopwatch.StartNew();
            var res= next(context, envelope);
            sw.Stop();

            if (sw.Elapsed > receiveDeadline)
            {
                logger.Log(logLevel,"Receive is taking too long {Elapsed} {Self} incoming message {Message}", sw.Elapsed, context.Self, envelope.Message.GetType().Name);
                Console.WriteLine($"Receive is taking too long {sw.Elapsed} {context.Self} incoming message {envelope.Message.GetType().Name}");
            }
                
            return res;
        };

        Props Outer(Props props)
        {
            props = inner(props);
            props = props.WithReceiverMiddleware(DeveloperReceiveLogging);

            return props;
        }

        return self with {ConfigureProps = Outer};
    }
}