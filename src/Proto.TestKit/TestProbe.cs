// -----------------------------------------------------------------------
// <copyright file="TestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <inheritdoc cref="ITestProbe" />
public class TestProbe : IActor, ITestProbe
{
    private readonly Channel<MessageAndSender> _messageChannel = Channel.CreateUnbounded<MessageAndSender>();

    private IContext? _context;

    /// <inheritdoc />
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started _:
                Context = context;

                break;
            case RequestReference _:
                if (context.Sender is not null)
                {
                    context.Respond(this);
                }

                break;
            case Terminated _:
                _messageChannel.Writer.TryWrite(new MessageAndSender(context));

                break;
            case SystemMessage _: return Task.CompletedTask;
            default:
                _messageChannel.Writer.TryWrite(new MessageAndSender(context));

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
            var item = _messageChannel.Reader.ReadAsync(cts.Token).AsTask().GetAwaiter().GetResult();
            throw new TestKitException(
                $"Waited {time.Seconds} seconds and received a message of type {item.Message?.GetType()}");
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    public object? GetNextMessage(TimeSpan? timeAllowed = null)
    {
        var time = timeAllowed ?? TimeSpan.FromSeconds(1);
        using var cts = new CancellationTokenSource(time);

        try
        {
            var output = _messageChannel.Reader.ReadAsync(cts.Token).AsTask().GetAwaiter().GetResult();
            Sender = output.Sender;
            return output.Message;
        }
        catch (OperationCanceledException)
        {
            throw new TestKitException($"Waited {time.Seconds} seconds but failed to receive a message");
        }
    }

    /// <inheritdoc />
    public T GetNextMessage<T>(TimeSpan? timeAllowed = null)
    {
        var output = GetNextMessage(timeAllowed);

        if (!(output is T))
        {
            throw new TestKitException($"Message expected type {typeof(T)}, actual type {output?.GetType()}");
        }

        return (T)output;
    }

    /// <inheritdoc />
    public T GetNextMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null)
    {
        var output = GetNextMessage<T>(timeAllowed);

        if (!when(output))
        {
            throw new TestKitException("Condition not met");
        }

        return output;
    }

    /// <inheritdoc />
    public async Task<object?> GetNextMessageAsync(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var time = timeAllowed ?? TimeSpan.FromSeconds(1);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(time);

        try
        {
            var output = await _messageChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
            Sender = output.Sender;
            return output.Message;
        }
        catch (OperationCanceledException)
        {
            throw new TestKitException($"Waited {time.Seconds} seconds but failed to receive a message");
        }
    }

    /// <inheritdoc />
    public async Task<T> GetNextMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default)
    {
        var output = await GetNextMessageAsync(timeAllowed, cancellationToken).ConfigureAwait(false);

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
        var output = await GetNextMessageAsync<T>(timeAllowed, cancellationToken).ConfigureAwait(false);

        if (!when(output))
        {
            throw new TestKitException("Condition not met");
        }

        return output;
    }

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
    public async IAsyncEnumerable<object?> ProcessMessagesAsync(TimeSpan? timeAllowed = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            object? message;

            try
            {
                message = await GetNextMessageAsync(timeAllowed, cancellationToken).ConfigureAwait(false);
            }
            catch (TestKitException)
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ProcessMessagesAsync<T>(TimeSpan? timeAllowed = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            T message;

            try
            {
                message = await FishForMessageAsync<T>(timeAllowed, cancellationToken).ConfigureAwait(false);
            }
            catch (TestKitException)
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ProcessMessagesAsync<T>(Func<T, bool> when,
        TimeSpan? timeAllowed = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            T message;

            try
            {
                message = await FishForMessageAsync(when, timeAllowed, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TestKitException)
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public T FishForMessage<T>(TimeSpan? timeAllowed = null) => FishForMessage<T>(_ => true, timeAllowed);

    /// <inheritdoc />
    public T FishForMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null)
    {
        var endTime = DateTime.UtcNow + (timeAllowed ?? TimeSpan.FromSeconds(1));

        while (DateTime.UtcNow < endTime)
        {
            var remaining = endTime - DateTime.UtcNow;
            using var cts = new CancellationTokenSource(remaining);

            try
            {
                var item = _messageChannel.Reader.ReadAsync(cts.Token).AsTask().GetAwaiter().GetResult();

                if (item.Message is T typed && when(typed))
                {
                    Sender = item.Sender;

                    return typed;
                }
            }
            catch (OperationCanceledException)
            {
                // try again until timeout
            }
        }

        throw new TestKitException("Message not found");
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
                var item = await _messageChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);

                if (item.Message is T typed && when(typed))
                {
                    Sender = item.Sender;
                    return typed;
                }
            }
            catch (OperationCanceledException)
            {
                // loop until timeout
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

    public static implicit operator TestProbe?(PID tpPid)
    {
        try
        {
            return TestKit.System.Root.RequestAsync<TestProbe>(tpPid, new RequestReference()).Result;
        }
        catch
        {
            return null;
        }
    }

    private class RequestReference
    {
    }
}