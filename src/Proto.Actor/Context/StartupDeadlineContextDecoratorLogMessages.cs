using System;
using Microsoft.Extensions.Logging;

namespace Proto.Context;

internal static partial class StartupDeadlineContextDecoratorLogMessages
{
    [LoggerMessage(0, LogLevel.Warning, "Actor {Self} deadline {Deadline}, exceeded on actor Started")]
    internal static partial void ActorDeadlineExceededOnStart(this ILogger logger, PID self, TimeSpan deadline);
}
