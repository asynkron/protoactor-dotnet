// // Copyright (c) Dolittle. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Proto;

internal static partial class ActorLoggingContextLogMessages
{
    [LoggerMessage(EventId = 0, Message = "Actor {Self} {ActorType} received message {MessageType}:{Message} from {Sender}")]
    internal static partial void ActorReceivedMessage(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, string sender);

    [LoggerMessage(EventId = 1, Message = "Actor {Self} {ActorType} completed message {MessageType}:{Message} from {Sender}")]
    internal static partial void ActorCompletedMessage(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, string sender);

    [LoggerMessage(EventId = 2, Message = "Actor {Self} {ActorType} failed during message {MessageType}:{Message} from {Sender}")]
    internal static partial void ActorFailedToCompleteMessage(this ILogger logger, LogLevel logLevel, Exception ex, PID self, string actorType, string messageType, object message, string sender);

    [LoggerMessage(EventId = 3, Message = "Actor {Self} {ActorType} ReenterAfter {Action}")]
    internal static partial void ActorReenterAfter(this ILogger logger, LogLevel logLevel, PID self, string actorType, string  action);

    [LoggerMessage(EventId = 4, Message = "Actor {Self} {ActorType} Sending RequestAsync {MessageType}:{Message} to {Target}")]
    internal static partial void ActorSendingRequestAsync(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, PID target);

    [LoggerMessage(EventId = 5, Message = "Actor {Self} {ActorType} Got response {Response} to {MessageType}:{Message} from {Target}")]
    internal static partial void ActorGotResponse(this ILogger logger, LogLevel logLevel, PID self, string actorType, object response, string messageType, object message, PID target);

    [LoggerMessage(EventId = 6, Message = "Actor {Self} {ActorType} Got exception waiting for RequestAsync response of {MessageType}:{Message} from {Target}")]
    internal static partial void ActorFailedWaitingForRequestAsync(this ILogger logger, LogLevel logLevel, Exception ex, PID self, string actorType, string messageType, object message, PID target);

    [LoggerMessage(EventId = 7, Message = "Actor {Self} {ActorType} Spawned child actor {Name} with PID {Pid}")]
    internal static partial void ActorSpawnedNamed(this ILogger logger, LogLevel logLevel, PID self, string actorType, string name, PID pid);

    [LoggerMessage(EventId = 8, Message = "Actor {Self} {ActorType} failed when spawning child actor {Name}")]
    internal static partial void ActorFailedSpawningChildActor(this ILogger logger, LogLevel logLevel, Exception ex, PID self, string actorType, string name);

    [LoggerMessage(EventId = 9, Message = "Actor {Self} {ActorType} responded with {MessageType}:{Message} to {Sender}")]
    internal static partial void ActorResponded(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, PID sender);

    [LoggerMessage(EventId = 10, Message = "Actor {Self} {ActorType} forwarded message to {Target}")]
    internal static partial void ActorForwarded(this ILogger logger, LogLevel logLevel, PID self, string actorType, PID target);

    [LoggerMessage(EventId = 11, Message = "Actor {Self} {ActorType} Sending Request {MessageType}:{Message} to {Target}")]
    internal static partial void ActorSendingRequest(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, PID target);

    [LoggerMessage(EventId = 12, Message = "Actor {Self} {ActorType} Sending {MessageType}:{Message} to {Target}")]
    internal static partial void ActorSending(this ILogger logger, LogLevel logLevel, PID self, string actorType, string messageType, object message, PID target);

    [LoggerMessage(EventId = 13, Message = "Actor {Self} {ActorType} Unwatching {Pid}")]
    internal static partial void ActorUnwatching(this ILogger logger, LogLevel logLevel, PID self, string actorType, PID pid);

    [LoggerMessage(EventId = 14, Message = "Actor {Self} {ActorType} Watching {Pid}")]
    internal static partial void ActorWatching(this ILogger logger, LogLevel logLevel, PID self, string actorType, PID pid);
}