using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Gossip;

internal static partial class GossiperLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Gossiper is not started, cannot set state for key {Key}")]
    internal static partial void GossiperNotStartedCannotSetState(this ILogger logger, string key);
}
