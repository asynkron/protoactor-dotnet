using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class OneForOneStrategyLogMessages
{
    // `reason` is kept so the logger captures the exception automatically
    [LoggerMessage(0, LogLevel.Information, "{Action} {Actor}")]
    internal static partial void OneForOneStrategyAction(this ILogger logger, string action, PID actor, Exception reason);
}
