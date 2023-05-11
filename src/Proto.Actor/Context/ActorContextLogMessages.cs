// // Copyright (c) Dolittle. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Proto.Context;

internal static partial class ActorContextLogMessages
{
    [LoggerMessage(0, LogLevel.Debug, "{Self} Responding to {Sender} with message {Message}")]
    internal static partial void ActorResponds(this ILogger logger, PID self, PID sender, object? message);

    [LoggerMessage(1, LogLevel.Warning, "{Self} Tried to respond but sender is null, with message {Message}")]
    internal static partial void ActorRespondsButSenderIsNull(this ILogger logger, PID self, object? message);

    [LoggerMessage(2, LogLevel.Error, "{Self} Failed to spawn child actor {Name}")]
    internal static partial void FailedToSpawnChildActor(this ILogger logger, PID self, string name);

    [LoggerMessage(3, LogLevel.Warning, "Message is null")]
    internal static partial void MessageIsNull(this ILogger logger);

    [LoggerMessage(4, LogLevel.Warning, "SystemMessage cannot be forwarded. {Message}")]
    internal static partial void SystemMessageCannotBeForwarded(this ILogger logger, object? message);

    [LoggerMessage(5, LogLevel.Error, "[Supervision] Actor {Self} : {ActorType} failed with message:{Message}")]
    internal static partial void EscalateFailure(this ILogger logger, Exception reason, PID self, string actorType, object? message);

    [LoggerMessage(6, LogLevel.Error, "Error handling SystemMessage {Message}")]
    internal static partial void ErrorHandlingSystemMessage(this ILogger logger, Exception ex, object? message);

    [LoggerMessage(7, LogLevel.Warning, "Unknown system message {Message}")]
    internal static partial void UnknownSystemMessage(this ILogger logger, object? message);

    [LoggerMessage(8, LogLevel.Warning, "{Self} Dropping Continuation (ReenterAfter) of {Message}")]
    internal static partial void DroppingContinuation(this ILogger logger, PID self, object? message);

    [LoggerMessage(9, LogLevel.Error, "{Self} Error while handling Stopping message")]
    internal static partial void ErrorHandlingStopingMessage(this ILogger logger, Exception ex, PID self);
}