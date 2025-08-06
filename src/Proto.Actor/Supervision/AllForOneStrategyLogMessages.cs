using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class AllForOneStrategyLogMessages
{
    [LoggerMessage(0, LogLevel.Information, "{Action} {Actor} because of {Reason}")]
    internal static partial void StrategyAction(this ILogger logger, string action, PID actor, Exception reason);
}
