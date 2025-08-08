using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class AllForOneStrategyLogMessages
{
    // "exception" parameter ensures the exception is logged automatically
    [LoggerMessage(0, LogLevel.Information, "{Action} {Actor}")]
    internal static partial void AllForOneStrategyAction(this ILogger logger, string action, PID actor, Exception exception);
}
