// -----------------------------------------------------------------------
// <copyright file="AmazonEcsProviderConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.AmazonECS;

[PublicAPI]
#pragma warning disable CA1801
public record AmazonEcsProviderConfig(int PollIntervalSeconds = 5, bool DeveloperLogging = false)
#pragma warning restore CA1801
{
    internal LogLevel DebugLogLevel => DeveloperLogging ? LogLevel.Information : LogLevel.Debug;
}