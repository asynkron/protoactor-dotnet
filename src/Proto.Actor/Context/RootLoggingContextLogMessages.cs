// // Copyright (c) Dolittle. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class RootLoggingContextLogMessages
{
    [LoggerMessage(EventId = 0, Message = "RootContext Sending {MessageType}:{Message} to {Target}")]
    internal static partial void RootContextSending(this ILogger logger, LogLevel level, string messageType, object message, PID target);

    [LoggerMessage(EventId = 1, Message = "RootContext Sending Request {MessageType}:{Message} to {Target}")]
    internal static partial void RootContextSendingRequest(this ILogger logger, LogLevel level, string messageType, object message, PID target);

    [LoggerMessage(EventId = 2, Message = "RootContext Stopping {Pid}")]
    internal static partial void RootContextStopping(this ILogger logger, LogLevel level, PID pid);

    [LoggerMessage(EventId = 3, Message = "RootContext Poisoning {Pid}")]
    internal static partial void RootContextPoisoning(this ILogger logger, LogLevel level, PID pid);

    [LoggerMessage(EventId = 4, Message = "RootContext Poisoned {Pid}")]
    internal static partial void RootContextPoisoned(this ILogger logger, LogLevel level, PID pid);

    [LoggerMessage(EventId = 5, Message = "RootContext Stopped {Pid}")]
    internal static partial void RootContextStopped(this ILogger logger, LogLevel level, PID pid);

    [LoggerMessage(EventId = 6, Message = "RootContext Spawned child actor {Name} with PID {Pid}")]
    internal static partial void RootContextSpawnedNamed(this ILogger logger, LogLevel level, string name, PID pid);

    [LoggerMessage(EventId = 7, Message = "RootContext Failed when spawning named child actor {Name}")]
    internal static partial void RootContextFailedToSpawnNamed(this ILogger logger, LogLevel level, Exception ex, string name);

    [LoggerMessage(EventId = 8, Message = "RootContext Sending RequestAsync {MessageType}:{Message} to {Target}")]
    internal static partial void RootContextSendingRequestAsync(this ILogger logger, LogLevel level, string messageType, object message, PID target);

    [LoggerMessage(EventId = 9, Message = "RootContext Got response {Response} to {MessageType}:{Message} from {Target}")]
    internal static partial void RootContextGotResponse(this ILogger logger, LogLevel level, object response, string messageType, object message, PID target);

    [LoggerMessage(EventId = 10, Message = "RootContext Got exception waiting for RequestAsync response of {MessageType}:{Message} from {Target}")]
    internal static partial void RootContextFailedSendingRequestAsync(this ILogger logger, LogLevel level, Exception ex, string messageType, object message, PID target);
}