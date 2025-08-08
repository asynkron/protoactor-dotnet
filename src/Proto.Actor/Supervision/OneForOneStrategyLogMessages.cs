using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class OneForOneStrategyLogMessages
{
    // "exception" parameter ensures the exception is logged automatically
    [LoggerMessage(0, LogLevel.Information, "{Action} {Actor}")]
    internal static partial void OneForOneStrategyAction(this ILogger logger, string action, PID actor, Exception exception);
}
