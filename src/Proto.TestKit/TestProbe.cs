﻿// -----------------------------------------------------------------------
// <copyright file="TestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <inheritdoc cref="ITestProbe" />
public class TestProbe : IActor, ITestProbe
{
    private readonly BlockingCollection<MessageAndSender>
        _messageQueue = new();

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
                _messageQueue.Add(new MessageAndSender(context));

                break;
            case SystemMessage _: return Task.CompletedTask;
            default:
                _messageQueue.Add(new MessageAndSender(context));

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

        if (_messageQueue.TryTake(out var o, time))
        {
            throw new TestKitException($"Waited {time.Seconds} seconds and received a message of type {o.GetType()}");
        }
    }

    /// <inheritdoc />
    public object? GetNextMessage(TimeSpan? timeAllowed = null)
    {
        var time = timeAllowed ?? TimeSpan.FromSeconds(1);

        if (!_messageQueue.TryTake(out var output, time))
        {
            throw new TestKitException($"Waited {time.Seconds} seconds but failed to receive a message");
        }

        Sender = output?.Sender;

        return output?.Message;
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
    public T FishForMessage<T>(TimeSpan? timeAllowed = null) => FishForMessage<T>(x => true, timeAllowed);

    /// <inheritdoc />
    public T FishForMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null)
    {
        var endTime = DateTime.UtcNow + (timeAllowed ?? TimeSpan.FromSeconds(1));

        while (DateTime.UtcNow < endTime)
        {
            if (_messageQueue.TryTake(out var item, endTime - DateTime.UtcNow) &&
                item.Message is T typed && when(typed))
            {
                Sender = item.Sender;

                return typed;
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