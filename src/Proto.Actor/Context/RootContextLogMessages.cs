using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class RootContextLogMessages
{
    [LoggerMessage(0, LogLevel.Error, "RootContext Failed to spawn root level actor {Name}")]
    internal static partial void FailedToSpawnRootActor(this ILogger logger, Exception exception, string name);
}
