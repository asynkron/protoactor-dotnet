using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class ActorSystemLogMessages
{
    [LoggerMessage(0, LogLevel.Warning, "System {Id} - ThreadPool is running hot, ThreadPool latency {ThreadPoolLatency}")]
    internal static partial void ThreadPoolRunningHot(this ILogger logger, string id, TimeSpan threadPoolLatency);

    [LoggerMessage(1, LogLevel.Information, "Shutting down actor system {Id} - Reason {Reason}")]
    internal static partial void ShuttingDownActorSystem(this ILogger logger, string id, string reason);
}
