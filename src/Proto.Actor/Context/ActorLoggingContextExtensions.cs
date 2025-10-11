using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto;

[PublicAPI]
public static class ActorLoggingContextExtensions
{
    public static IRootContext WithLoggingContext(this IRootContext context, ILogger logger) => new RootLoggingContext(context, logger);
    public static Props WithLoggingContextDecorator(
        this Props props,
        ILogger logger,
        LogLevel logLevel = LogLevel.Debug,
        LogLevel infrastructureLogLevel = LogLevel.None,
        LogLevel exceptionLogLevel = LogLevel.Error
    ) =>
        props.WithContextDecorator(ctx =>
            new ActorLoggingContext(ctx, logger, logLevel, infrastructureLogLevel, exceptionLogLevel));
}