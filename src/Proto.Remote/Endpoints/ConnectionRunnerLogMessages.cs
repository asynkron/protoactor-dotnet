using System;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

internal static partial class ConnectionRunnerLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "[ServerConnector][{SystemAddress}] Connecting to {Address}")]
    internal static partial void Connecting(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "[ServerConnector][{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, we are blocked")]
    internal static partial void ConnectionRefusedWeAreBlocked(this ILogger logger, Exception ex, string systemAddress, string memberId, string address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "[ServerConnector][{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, they are blocked")]
    internal static partial void ConnectionRefusedTheyAreBlocked(this ILogger logger, Exception ex, string systemAddress, string memberId, string address);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[ServerConnector][{SystemAddress}] Connected to {Address}")]
    internal static partial void Connected(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[ServerConnector][{SystemAddress}] Disconnected from {Address}")]
    internal static partial void Disconnected(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "[ServerConnector][{SystemAddress}] dropped connection to blocked member {ActorSystemId}/{Address}")]
    internal static partial void DroppedBlockedConnection(this ILogger logger, string systemAddress, string actorSystemId, string address);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "[ServerConnector][{SystemAddress}] Stopping connection to {Address} after retries expired because the endpoint is unavailable")]
    internal static partial void StoppingUnavailable(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "[ServerConnector][{SystemAddress}] Stopping connection to {Address} after retries expired because of {ExceptionType}")]
    internal static partial void StoppingBecauseException(this ILogger logger, Exception ex, string systemAddress, string address, string exceptionType);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "[ServerConnector][{SystemAddress}] Restarting endpoint connection to {Address} after {Duration} because of {ExceptionType} ({FailureCount} / {MaxRetries})")]
    internal static partial void RestartingEndpoint(this ILogger logger, string systemAddress, string address, TimeSpan Duration, string ExceptionType, int FailureCount, int MaxRetries);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "[ServerConnector][{SystemAddress}] Writer cancelled for {Address}")]
    internal static partial void WriterCancelled(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "[ServerConnector][{SystemAddress}] Received disconnection request from {Address}")]
    internal static partial void ReceivedDisconnectionRequest(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "[ServerConnector][{SystemAddress}] Reader finished for {Address}")]
    internal static partial void ReaderFinished(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "[ServerConnector][{SystemAddress}] Reader cancelled for {Address}")]
    internal static partial void ReaderCancelledDebug(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "[ServerConnector][{SystemAddress}] Reader cancelled for {Address}")]
    internal static partial void ReaderCancelledWarning(this ILogger logger, string systemAddress, string address);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning, Message = "[ServerConnector][{SystemAddress}] Error in reader for {Address} {ExceptionType}")]
    internal static partial void ReaderError(this ILogger logger, string systemAddress, string address, string exceptionType);
}
