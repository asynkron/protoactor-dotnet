// -----------------------------------------------------------------------
// <copyright file="TestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <inheritdoc />
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started _:
                Context = context;

                break;
            case Terminated _:
                _channel.Writer.TryWrite(new MessageAndSender(context));

                break;
            case SystemMessage _: return Task.CompletedTask;
            default:
                _channel.Writer.TryWrite(new MessageAndSender(context));

                break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public PID? Sender { get; private set; }

    /// <inheritdoc />
    public IContext Context
    {
        get
        {
            if (_context is null)
            {
                throw new InvalidOperationException("Probe context is null");
            }

            return _context;
        }
        private set => _context = value;
    }

    /// <inheritdoc />
    public void ExpectNoMessage(TimeSpan? timeAllowed = null)
    {
        var time = timeAllowed ?? TimeSpan.FromSeconds(1);

        using var cts = new CancellationTokenSource(time);

        try
        {
            var item = _channel.Reader.ReadAsync(cts.Token).AsTask().GetAwaiter().GetResult();
            var seconds = time.TotalSeconds.ToString("0.###");
            throw new TestKitException($"Waited {seconds} seconds and received a message of type {item.Message.GetType()}");
        }
        catch (OperationCanceledException)
        {
            // expected - no message arrived
        }
    }

    /// <inheritdoc />
    public object? GetNextMessage(TimeSpan? timeAllowed = null) =>
        GetNextMessageAsync(timeAllowed).GetAwaiter().GetResult();

    /// <inheritdoc />
    public T GetNextMessage<T>(TimeSpan? timeAllowed = null) =>
        GetNextMessageAsync<T>(timeAllowed).GetAwaiter().GetResult();

    /// <inheritdoc />
    public T GetNextMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null) =>
        GetNextMessageAsync(when, timeAllowed).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IEnumerable ProcessMessages(TimeSpan? timeAllowed = null)
    {
        while (true)
        {
            object? message;

            try
            {
                message = GetNextMessage(timeAllowed);
            }
            catch
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public IEnumerable<T> ProcessMessages<T>(TimeSpan? timeAllowed = null)
    {
        while (true)
        {
            T message;

            try
            {
                message = FishForMessage<T>(timeAllowed);
            }
            catch
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public IEnumerable<T> ProcessMessages<T>(Func<T, bool> when, TimeSpan? timeAllowed = null)
    {
        while (true)
        {
            T message;

            try
            {
                message = FishForMessage(when, timeAllowed);
            }
            catch
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public T FishForMessage<T>(TimeSpan? timeAllowed = null) =>
        FishForMessageAsync<T>(timeAllowed).GetAwaiter().GetResult();

    /// <inheritdoc />
    public T FishForMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null) =>
        FishForMessageAsync(when, timeAllowed).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<object?> GetNextMessageAsync(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeAllowed ?? TimeSpan.FromSeconds(1));

        try
        {
            var item = await _channel.Reader.ReadAsync(cts.Token);
            Sender = item.Sender;

            return item.Message;
        }
        catch (OperationCanceledException)
        {
            var seconds = (timeAllowed ?? TimeSpan.FromSeconds(1)).TotalSeconds.ToString("0.###");
            throw new TestKitException($"Waited {seconds} seconds but failed to receive a message");
        }
    }

    /// <inheritdoc />
    public async Task<T> GetNextMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var output = await GetNextMessageAsync(timeAllowed, cancellationToken);

        if (output is not T typed)
        {
            throw new TestKitException($"Message expected type {typeof(T)}, actual type {output?.GetType()}");
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

    public static implicit operator PID?(TestProbe tp) => tp.Context.Self;
}