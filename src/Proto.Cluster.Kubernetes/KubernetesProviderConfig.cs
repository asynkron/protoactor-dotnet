// -----------------------------------------------------------------------
// <copyright file="KubernetesProviderConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Kubernetes
{
    public record KubernetesProviderConfig
    {
        public int WatchTimeoutSeconds { get; }
        private bool DeveloperLogging { get; }

        public KubernetesProviderConfig(int watchTimeoutSeconds = 30, bool developerLogging = false)
        {
            WatchTimeoutSeconds = watchTimeoutSeconds;
            DeveloperLogging = developerLogging;
        }
        
        internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;
    }
}