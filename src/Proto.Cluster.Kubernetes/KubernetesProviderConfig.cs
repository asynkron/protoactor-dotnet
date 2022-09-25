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
    public KubernetesProviderConfig(int watchTimeoutSeconds = 30, bool developerLogging = false,
        Func<IKubernetes> clientFactory = null)
    {
        WatchTimeoutSeconds = watchTimeoutSeconds;
        DeveloperLogging = developerLogging;
        ClientFactory = clientFactory ?? DefaultFactory;
    }

    /// <summary>
    ///     A timeout for the watch pods operation
    /// </summary>
    public int WatchTimeoutSeconds { get; }

    /// <summary>
    ///     Enables more detailed logging
    /// </summary>
    private bool DeveloperLogging { get; }

    /// <summary>
    ///     Override the default implementation to configure the kubernetes client
    /// </summary>
    public Func<IKubernetes> ClientFactory { get; }

    internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;

    private static IKubernetes DefaultFactory() => new k8s.Kubernetes(KubernetesClientConfiguration.InClusterConfig());
}