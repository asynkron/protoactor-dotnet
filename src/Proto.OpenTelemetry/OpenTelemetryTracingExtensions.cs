using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Proto.OpenTelemetry;

/// <summary>
///     Customizes the activity based on a message being processed
///     <param name="activity">Activity to be customized</param>
///     <param name="message">Message being processed</param>
/// </summary>
public delegate void ActivitySetup(Activity activity, object message);

public static class OpenTelemetryTracingExtensions
{
    public static TracerProviderBuilder AddProtoActorInstrumentation(this TracerProviderBuilder builder) =>
        builder.AddSource(ProtoTags.ActivitySourceName);

    /// <summary>
    ///     Adds OpenTelemetry tracing to actors spawned with given <see cref="Props" />. Incoming and outgoing messages will
    ///     create new activities.
    ///     Ensures <see cref="Activity" /> context propagation via message headers.
    /// </summary>
    /// <param name="props"><see cref="Props" /> to instrument</param>
    /// <param name="sendActivitySetup">Optional delegate to customize the <see cref="Activity" /> on message receive</param>
    /// <param name="receiveActivitySetup">Optional delegate to customize the <see cref="Activity" /> on message send</param>
    /// <returns>props</returns>
    public static Props WithTracing(
        this Props props,
        ActivitySetup? sendActivitySetup = null,
        ActivitySetup? receiveActivitySetup = null
    )
    {
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        sendActivitySetup ??= OpenTelemetryHelpers.DefaultSetupActivity!;
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        receiveActivitySetup ??= OpenTelemetryHelpers.DefaultSetupActivity!;

        return props
            .WithContextDecorator(ctx =>
                new OpenTelemetryActorContextDecorator(ctx, sendActivitySetup, receiveActivitySetup))
            .WithSenderMiddleware(OpenTelemetrySenderMiddleware);
    }

    /// <summary>
    ///     Adds trace headers to the message envelope, to propagate trace context.
    /// </summary>
    public static Sender OpenTelemetrySenderMiddleware(Sender next) =>
        async (context, target, envelope) =>
        {
            var activity = Activity.Current;

            if (activity != null)
            {
                envelope = envelope.WithHeaders(activity.Context.GetPropagationHeaders());
            }

            await next(context, target, envelope).ConfigureAwait(false);
        };

    /// <summary>
    ///     Adds OpenTelemetry tracing to messages sent through <see cref="IRootContext" />. Sent messages will create new
    ///     activities.
    ///     Ensures <see cref="Activity" /> context propagation via message headers.
    /// </summary>
    /// <param name="context"><see cref="IRootContext" /> to instrument</param>
    /// <param name="sendActivitySetup">Optional delegate to customize the <see cref="Activity" /> on message send</param>
    /// <returns></returns>
    public static IRootContext WithTracing(this IRootContext context, ActivitySetup? sendActivitySetup = null)
    {
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        sendActivitySetup ??= OpenTelemetryHelpers.DefaultSetupActivity!;

        return new OpenTelemetryRootContextDecorator(context.WithSenderMiddleware(OpenTelemetrySenderMiddleware),
            sendActivitySetup);
    }
}