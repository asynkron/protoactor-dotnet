using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class SystemContextLogMessages
{
    [LoggerMessage(0, LogLevel.Error, "SystemContext Failed to spawn system actor {Name}")]
    internal static partial void FailedToSpawnSystemActor(this ILogger logger, string name);

    [LoggerMessage(1, LogLevel.Error, "SystemContext Failed to spawn system actor {Name}")]
    internal static partial void FailedToSpawnSystemActor(this ILogger logger, Exception exception, string name);
}
