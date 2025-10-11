using System;
using Microsoft.Extensions.Logging;

namespace Proto.DependencyInjection;

internal static partial class DependencyResolverLogMessages
{
    [LoggerMessage(0, LogLevel.Error, "DependencyResolved Failed resolving Props for actor type {ActorType}")]
    internal static partial void FailedResolvingProps(this ILogger logger, Exception exception, string actorType);
}
