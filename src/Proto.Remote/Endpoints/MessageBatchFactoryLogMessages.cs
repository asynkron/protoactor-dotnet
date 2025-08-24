using Microsoft.Extensions.Logging;
using Proto;

namespace Proto.Remote;

internal static partial class MessageBatchFactoryLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Null message passed to EndpointActor for target {Target}, ignoring message")]
    internal static partial void EndpointActorReceivedNullMessage(this ILogger logger, PID target);
}
