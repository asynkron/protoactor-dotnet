using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class SenderContextLogMessages
{
    [LoggerMessage(0, LogLevel.Error, "Context {Self} got DeadLetterResponse for PID {Pid}")]
    internal static partial void ContextGotDeadLetterResponse(this ILogger logger, PID self, PID pid);
}
