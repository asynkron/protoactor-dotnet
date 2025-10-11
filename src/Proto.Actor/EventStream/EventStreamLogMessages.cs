using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class EventStreamLogMessages
{
    [LoggerMessage(0, LogLevel.Information, "[DeadLetter] Throttled {LogCount} logs")]
    internal static partial void DeadLetterThrottled(this ILogger logger, int logCount);

    [LoggerMessage(1, LogLevel.Information, "[DeadLetter] could not deliver '{MessageType}:{MessagePayload}' to '{Target}' from '{Sender}'")]
    internal static partial void DeadLetter(this ILogger logger, string messageType, object? messagePayload, PID target, PID? sender);

    [LoggerMessage(2, LogLevel.Error, "Exception has occurred when publishing a message")]
    internal static partial void ExceptionWhenPublishingMessage(this ILogger logger, Exception exception);
}
