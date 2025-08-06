using System;
using Microsoft.Extensions.Logging;

namespace Proto.Context;

internal static partial class DeadlineContextDecoratorLogMessages
{
    [LoggerMessage(0, LogLevel.Warning, "Actor {Self} deadline {Deadline}, exceeded on message {MessagePayload}")]
    internal static partial void ActorDeadlineExceededOnMessage(this ILogger logger, PID self, TimeSpan deadline, object? messagePayload);
}
