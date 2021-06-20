// -----------------------------------------------------------------------
// <copyright file="ActorSystemConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using JetBrains.Annotations;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.NoMetrics;

// ReSharper disable once CheckNamespace
namespace Proto
{
    [PublicAPI]
    public record ActorSystemConfig
    {
        public TimeSpan DeadLetterThrottleInterval { get; init; }

        public IMetricsProvider[] MetricsProviders { get; init; } = {new NoMetricsProvider()};
        public int DeadLetterThrottleCount { get; init; }

        public bool DeadLetterRequestLogging { get; set; } = true;
        public bool DeveloperSupervisionLogging { get; init; }

        public Func<Props, Props> ConfigureProps { get; init; } = props => props; 

        public bool SharedFutures { get; init; }
        public int SharedFutureSize { get; init; } = 5000;

        public TimeSpan ThreadPoolStatsTimeout { get; init; } = TimeSpan.FromSeconds(1);
        public bool DeveloperThreadPoolStatsLogging { get; init; }

        public static ActorSystemConfig Setup() => new();

        public Func<IActor, string> DiagnosticsSerializer { get; set; } = Diagnostics.DiagnosticsSerializer.Serialize;

        public ActorSystemConfig WithDeadLetterThrottleInterval(TimeSpan deadLetterThrottleInterval) =>
            this with {DeadLetterThrottleInterval = deadLetterThrottleInterval};

        public ActorSystemConfig WithDeadLetterThrottleCount(int deadLetterThrottleCount) =>
            this with {DeadLetterThrottleCount = deadLetterThrottleCount};

        public ActorSystemConfig WithDeadLetterRequestLogging(bool enabled) => this with {DeadLetterRequestLogging = enabled};

        public ActorSystemConfig WithSharedFutures(int size = 5000) => this with {SharedFutures = true, SharedFutureSize = size};

        public ActorSystemConfig WithDeveloperSupervisionLogging(bool enabled) => this with {DeveloperSupervisionLogging = enabled};

        public ActorSystemConfig WithMetricsProviders(params IMetricsProvider[] providers) => this with {MetricsProviders = providers};

        public ActorSystemConfig WithDiagnosticsSerializer(Func<IActor, string> serializer) => this with {DiagnosticsSerializer = serializer};
        
        public ActorSystemConfig WithConfigureProps(Func<Props, Props> configureProps) => this with {ConfigureProps = configureProps};
        
        public ActorSystemConfig WithThreadPoolStatsTimeout(TimeSpan threadPoolStatsTimeout) => this with {ThreadPoolStatsTimeout = threadPoolStatsTimeout};
        
        public ActorSystemConfig WithDeveloperThreadPoolStatsLogging(bool enabled) => this with {DeveloperThreadPoolStatsLogging = enabled};
        
    }

    //Not part of the contract, but still shipped out of the box
    public static class ActorSystemConfigExtensions
    {
        public static ActorSystemConfig WithDeveloperReceiveLogging(this ActorSystemConfig self, TimeSpan receiveDeadline)
        {
            var inner = self.ConfigureProps;
            
            Receiver DeveloperReceiveLogging(Receiver next) => (context, envelope) => {
                var sw = Stopwatch.StartNew();
                var res= next(context, envelope);
                var actorContext = (IContext) context;
                var currentMessage = actorContext.Message;
                sw.Stop();

                if (sw.Elapsed > receiveDeadline)
                {
                    Console.WriteLine($"Receive is taking too long {context.Self} incoming message {envelope.Message}, current message {currentMessage}");
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
}