using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Timers;

/// <summary>
///     Scheduler can be used to schedule a message to be sent in the future. It is useful e.g., when actor needs to do
///     some work after a certain time.
/// </summary>
/// <remarks>
///     The user is responsible for cancelling sends. They will not be automatically
///     cancelled when this object is destroyed and will be left around in the background.
/// </remarks>
[PublicAPI]
public class Scheduler
{
    private readonly ISenderContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ISchedulerHook? _schedulerHook;

    /// <summary>
    ///     Creates a new scheduler.
    /// </summary>
    /// <param name="context">Context to send the scheduled message through</param>
    public Scheduler(ISenderContext context, ISchedulerHook? schedulerHook = null)
    {
        _context = context;
        _timeProvider = TimeProvider.System;
        _schedulerHook = schedulerHook;
    }

    /// <summary>
    ///     Creates a new scheduler.
    /// </summary>
    /// <param name="context">Context to send the scheduled message through</param>
    /// <param name="timeProvider">TimeProvider to use for scheduling (FakeTimeProvider can be used for testing)</param>
    public Scheduler(ISenderContext context, TimeProvider timeProvider, ISchedulerHook? schedulerHook = null)
    {
        _context = context;
        _timeProvider = timeProvider;
        _schedulerHook = schedulerHook;
    }

    /// <summary>
    ///     Schedules a single message to be sent in the future.
    /// </summary>
    /// <param name="delay">Delay before sending the message</param>
    /// <param name="target"><see cref="PID" /> of the recipient.</param>
    /// <param name="message">Message to be sent</param>
    /// <returns><see cref="CancellationTokenSource" /> that can be used to cancel the scheduled message</returns>
    public CancellationTokenSource SendOnce(TimeSpan delay, PID target, object message)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _ = SafeTask.Run(async () =>
            {
                await Delay(delay, token).ConfigureAwait(false);

                _context.Send(target, message);
            }, token
        );

        return cts;
    }

    /// <summary>
    ///     Schedules message sending on a periodic basis.
    /// </summary>
    /// <param name="interval">Interval between sends, and also the initial delay</param>
    /// <param name="target"><see cref="PID" /> of the recipient.</param>
    /// <param name="message">Message to be sent</param>
    /// <returns><see cref="CancellationTokenSource" /> that can be used to cancel the scheduled messages</returns>
    public CancellationTokenSource SendRepeatedly(TimeSpan interval, PID target, object message) =>
        SendRepeatedly(interval, interval, target, message);

    /// <summary>
    ///     Schedules message sending on a periodic basis.
    /// </summary>
    /// <param name="delay">Initial delay</param>
    /// <param name="interval">Interval between sends</param>
    /// <param name="target"><see cref="PID" /> of the recipient.</param>
    /// <param name="message">Message to be sent</param>
    /// <returns><see cref="CancellationTokenSource" /> that can be used to cancel the scheduled messages</returns>
    public CancellationTokenSource SendRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _ = SafeTask.Run(async () =>
            {
                await Delay(delay, token).ConfigureAwait(false);

                while (!cts.IsCancellationRequested)
                {
                    _context.Send(target, message);

                    await Delay(interval, token).ConfigureAwait(false);
                }
            }, token
        );

        return cts;
    }

    /// <summary>
    ///     Schedules a request on a periodic basis. The response will arrive to the actor context for which the
    ///     <see cref="Scheduler" /> was created.
    /// </summary>
    /// <param name="delay">Initial delay</param>
    /// <param name="interval">Interval between requests</param>
    /// <param name="target"><see cref="PID" /> of the recipient.</param>
    /// <param name="message">Message to be sent</param>
    /// <returns><see cref="CancellationTokenSource" /> that can be used to cancel the scheduled messages</returns>
    public CancellationTokenSource RequestRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _ = SafeTask.Run(async () =>
            {
                await Delay(delay, token).ConfigureAwait(false);

                while (!cts.IsCancellationRequested)
                {
                    _context.Request(target, message);

                    await Delay(interval, token).ConfigureAwait(false);
                }
            }, token
        );

        return cts;
    }

    private async Task Delay(TimeSpan delay, CancellationToken token)
    {
        var delayTask = Task.Delay(delay, _timeProvider, token);
        _schedulerHook?.OnTimerRegistered();
        await delayTask.ConfigureAwait(false);
    }
}
