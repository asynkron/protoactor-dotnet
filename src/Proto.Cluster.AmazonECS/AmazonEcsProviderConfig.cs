// -----------------------------------------------------------------------
// <copyright file="AmazonEcsProviderConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.AmazonECS
{
    [PublicAPI]
    public record AmazonEcsProviderConfig(int PollIntervalSeconds = 5, bool DeveloperLogging= false)
    {
        internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;
    }
}