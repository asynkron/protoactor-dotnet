using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class TaskFactoryLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Unhandled exception in async job {Job}")]
    internal static partial void UnhandledExceptionInAsyncJob(this ILogger logger, Exception exception, string job);
}
