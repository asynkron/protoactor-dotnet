using Microsoft.Extensions.Logging;

namespace Proto.Context;

internal static partial class BatchContextLogMessages
{
    [LoggerMessage(0, LogLevel.Warning, "Batch request got {AdditionalCalls} more calls than provisioned")]
    internal static partial void BatchRequestGotAdditionalCalls(this ILogger logger, int additionalCalls);

    [LoggerMessage(1, LogLevel.Error, "BatchContext {Self} got DeadLetterResponse for PID {Pid}")]
    internal static partial void BatchContextGotDeadLetter(this ILogger logger, PID self, PID pid);
}
