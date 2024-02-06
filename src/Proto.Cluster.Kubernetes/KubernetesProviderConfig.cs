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
        Func<IKubernetes> clientFactory = null, bool disableWatch = false)
    {
        WatchTimeoutSeconds = watchTimeoutSeconds;
        DeveloperLogging = developerLogging;
        ClientFactory = clientFactory ?? DefaultFactory;
        DisableWatch = disableWatch;
    }

    /// <summary>
    ///     A timeout for the watch pods operation
    /// </summary>
    public int WatchTimeoutSeconds { get; }

    /// <summary>
    ///    Disable the watch pods operation and rely on HTTP request response polling instead
    /// </summary>
    public bool DisableWatch { get; set; }

    /// <summary>
    ///     Enables more detailed logging
    /// </summary>
    private bool DeveloperLogging { get; }
    
    /// <summary>
    /// The k8s Cluster Domain (TLD), defaults to "cluster.local"
    /// </summary>
    public string ClusterDomain { get; init; } = "cluster.local";

    /// <summary>
    ///     Override the default implementation to configure the kubernetes client
    /// </summary>
    public Func<IKubernetes> ClientFactory { get; }

    internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;

    internal static IKubernetes DefaultFactory() => new k8s.Kubernetes(KubernetesClientConfiguration.InClusterConfig());
    
    /// <summary>
    /// The k8s Cluster Domain (TLD), defaults to "cluster.local"
    /// </summary>
    public KubernetesProviderConfig WithClusterDomain(string clusterDomain = "cluster.local")
        => this with { ClusterDomain = clusterDomain };
}