using Microsoft.Extensions.Logging;

namespace Proto.Deduplication;

internal static partial class DeDuplicatorLogMessages
{
    [LoggerMessage(0, LogLevel.Information, "Request de-duplicated")]
    internal static partial void RequestDeduplicated(this ILogger logger);
}
