using System;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

internal static partial class EndpointManagerLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "[{SystemAddress}] Stopping")]
    internal static partial void Stopping(this ILogger logger, string systemAddress);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "[{SystemAddress}] Stopped")]
    internal static partial void Stopped(this ILogger logger, string systemAddress);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "[{SystemAddress}] Endpoint {Address} terminating")]
    internal static partial void EndpointTerminating(this ILogger logger, string systemAddress, string? address);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[{SystemAddress}] Endpoint {Address} terminated")]
    internal static partial void EndpointTerminated(this ILogger logger, string systemAddress, string? address);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "[{SystemAddress}] Endpoint {Address} already removed.")]
    internal static partial void EndpointAlreadyRemoved(this ILogger logger, string systemAddress, string? address);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "[{SystemAddress}] Error during endpoint {Address} termination")]
    internal static partial void ErrorDuringEndpointTermination(this ILogger logger, Exception ex, string systemAddress, string? address);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "[{SystemAddress}] Tried to get endpoint for null address")]
    internal static partial void TriedGetEndpointForNullAddress(this ILogger logger, string systemAddress);
}

