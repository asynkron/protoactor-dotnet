using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class EventProbeLogMessages
{
    [LoggerMessage(0, LogLevel.Debug, "Setting expectation")]
    internal static partial void SettingExpectation(this ILogger logger);

    [LoggerMessage(1, LogLevel.Debug, "Got expected event {Event}")]
    internal static partial void GotExpectedEvent(this ILogger logger, object @event);

    [LoggerMessage(2, LogLevel.Debug, "Got unexpected {Event}, ignoring")]
    internal static partial void GotUnexpectedEvent(this ILogger logger, object @event);
}
