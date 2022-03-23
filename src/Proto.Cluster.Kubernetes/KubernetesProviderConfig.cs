// -----------------------------------------------------------------------
// <copyright file="KubernetesProviderConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using k8s;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Kubernetes;

public record KubernetesProviderConfig
{
    public int WatchTimeoutSeconds { get; }
    private bool DeveloperLogging { get; }
    
    public Func<IKubernetes> ClientFactory { get; }

    public KubernetesProviderConfig(int watchTimeoutSeconds = 30, bool developerLogging = false, Func<IKubernetes> clientFactory = null)
    {
        WatchTimeoutSeconds = watchTimeoutSeconds;
        DeveloperLogging = developerLogging;
        ClientFactory = clientFactory ?? DefaultFactory;
    }

    private static IKubernetes DefaultFactory() => new k8s.Kubernetes(KubernetesClientConfiguration.InClusterConfig());

    internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;
}