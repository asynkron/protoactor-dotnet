// -----------------------------------------------------------------------
// <copyright file="TestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <inheritdoc cref="ITestProbe" />
public class TestProbe : IActor, ITestProbe
{
    private readonly Channel<MessageAndSender> _channel = Channel.CreateUnbounded<MessageAndSender>();

    private IContext? _context;
    // Tracks if the probe has processed its own startup.
    private bool _started;

    /// <inheritdoc />
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started _ when !_started:
                Context = context;
                _started = true;

                break;
            case Started _:
                _channel.Writer.TryWrite(new MessageAndSender(context));

                break;
            case Terminated _:
                _channel.Writer.TryWrite(new MessageAndSender(context));

                break;
            case SystemMessage _:
            default:
                _channel.Writer.TryWrite(new MessageAndSender(context));

                break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public PID? Sender { get; private set; }

    /// <summary>
    ///     The PID of this probe.
    /// </summary>
    public PID Self => Context.Self;

    private IContext Context
    {
        get
        {
            // Spin-wait for up to one second for the probe's context to be set.
            if (!SpinWait.SpinUntil(() => _context != null, TimeSpan.FromSeconds(1)))
            {
                throw new InvalidOperationException("Probe context is null");
            }

            return _context!;
        }
        set => _context = value;
    }

    /// <inheritdoc />
    public async Task ExpectNoMessageAsync(TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeAllowed ?? TimeSpan.FromSeconds(1));

        try
        {
            var item = await _channel.Reader.ReadAsync(cts.Token);
            var seconds = (timeAllowed ?? TimeSpan.FromSeconds(1)).TotalSeconds.ToString("0.###");
            throw new TestKitException($"Waited {seconds} seconds and received a message of type {item.Message?.GetType()}");
        }
        catch (OperationCanceledException)
        {
            // expected - no message arrived
        }
    }

    /// <inheritdoc />
    public async Task<object?> GetNextMessageAsync(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var item = await ReceiveNextAsync(timeAllowed, cancellationToken);

        return item.Message;
    }

    /// <inheritdoc />
    public async Task<T> GetNextMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var item = await ReceiveNextAsync(timeAllowed, cancellationToken);

        if (item.Message is not T typed)
        {
            throw new TestKitException($"Message expected type {typeof(T)}, actual type {item.Message?.GetType()}");
        }

        return typed;
    }

    /// <inheritdoc />
    public async Task<T> GetNextMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var output = await GetNextMessageAsync<T>(timeAllowed, cancellationToken);

        if (!when(output))
        {
            throw new TestKitException("Condition not met");
        }

        return output;
    }

    /// <inheritdoc />
    public Task<T> FishForMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) =>
        FishForMessageAsync<T>(_ => true, timeAllowed, cancellationToken);

    /// <inheritdoc />
    public async Task<T> FishForMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow + (timeAllowed ?? TimeSpan.FromSeconds(1));

        while (DateTime.UtcNow < endTime)
        {
            var remaining = endTime - DateTime.UtcNow;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(remaining);

            try
            {
                var item = await _channel.Reader.ReadAsync(cts.Token);

                if (item.Message is T typed && when(typed))
                {
                    Sender = item.Sender;

                    return typed;
                }
            }
            catch (OperationCanceledException)
            {
                // ignored, loop will end if time elapsed
            }
        }

        throw new TestKitException("Message not found");
    }

    /// <inheritdoc />
    public void Send(PID target, object message) => Context.Send(target, message);

    /// <inheritdoc />
    public void Request(PID target, object message) => Context.Request(target, message);

    /// <inheritdoc />
    public void Respond(object message)
    {
        if (Sender is null)
        {
            return;
        }

        Send(Sender, message);
    }

    /// <inheritdoc />
    public Task<T> RequestAsync<T>(PID target, object message) => Context.RequestAsync<T>(target, message);

    /// <inheritdoc />
    public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
        Context.RequestAsync<T>(target, message, cancellationToken);

    /// <inheritdoc />
    public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeAllowed) =>
        Context.RequestAsync<T>(target, message, timeAllowed);

    public void Watch(PID pid) => Context.Watch(pid);

    public void Unwatch(PID pid) => Context.Unwatch(pid);


    private async Task<MessageAndSender> ReceiveNextAsync(TimeSpan? timeAllowed,
        CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeAllowed ?? TimeSpan.FromSeconds(1));

        try
        {
            var item = await _channel.Reader.ReadAsync(cts.Token);
            Sender = item.Sender;
            return item;
        }
        catch (OperationCanceledException)
        {
            var seconds = (timeAllowed ?? TimeSpan.FromSeconds(1)).TotalSeconds.ToString("0.###");
            throw new TestKitException($"Waited {seconds} seconds but failed to receive a message");
        }
    }

    /// <inheritdoc />
    public async Task<T> GetNextSystemMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage
    {
        var item = await ReceiveNextAsync(timeAllowed, cancellationToken);

        if (item.Message is not SystemMessage)
        {
            throw new TestKitException(
                $"Expected system message of type {typeof(T)}, but received user message of type {item.Message?.GetType()}");
        }

        if (item.Message is not T typed)
        {
            throw new TestKitException($"Message expected type {typeof(T)}, actual type {item.Message?.GetType()}");
        }

        return typed;
    }

    /// <inheritdoc />
    public async Task<T> GetNextUserMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var item = await ReceiveNextAsync(timeAllowed, cancellationToken);

        if (item.Message is SystemMessage sys)
        {
            throw new TestKitException(
                $"Expected user message of type {typeof(T)}, but received system message of type {sys.GetType()}");
        }

        if (item.Message is not T typed)
        {
            throw new TestKitException($"Message expected type {typeof(T)}, actual type {item.Message?.GetType()}");
        }

        return typed;
    }

    /// <inheritdoc />
    public async Task<T> GetNextSystemMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage
    {
        var output = await GetNextSystemMessageAsync<T>(timeAllowed, cancellationToken);

        if (!when(output))
        {
            throw new TestKitException("Condition not met");
        }

        return output;
    }

    /// <inheritdoc />
    public async Task<T> GetNextUserMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var output = await GetNextUserMessageAsync<T>(timeAllowed, cancellationToken);

        if (!when(output))
        {
            throw new TestKitException("Condition not met");
        }

        return output;
    }

    /// <inheritdoc />
    public async Task ExpectNextSystemMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage =>
        _ = await GetNextSystemMessageAsync<T>(timeAllowed, cancellationToken);

    /// <inheritdoc />
    public async Task ExpectNextUserMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) =>
        _ = await GetNextUserMessageAsync<T>(timeAllowed, cancellationToken);

    /// <inheritdoc />
    public async Task ExpectNextSystemMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage =>
        _ = await GetNextSystemMessageAsync(when, timeAllowed, cancellationToken);

    /// <inheritdoc />
    public async Task ExpectNextUserMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) =>
        _ = await GetNextUserMessageAsync(when, timeAllowed, cancellationToken);

    /// <inheritdoc />
    public async Task ExpectEmptyMailboxAsync(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var self = Context.Self;

        if (timeAllowed.HasValue)
        {
            await RequestAsync<Touched>(self, new Touch(), timeAllowed.Value);
        }
        else
        {
            await RequestAsync<Touched>(self, new Touch(), cancellationToken);
        }

        await GetNextUserMessageAsync<Touch>(timeAllowed, cancellationToken);
    }
}