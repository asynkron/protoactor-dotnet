// -----------------------------------------------------------------------
// <copyright file="EventStream.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using Proto.Mailbox;
using Proto.Utils;

namespace Proto;

/// <summary>
///     Event stream global to an actor system.
/// </summary>
[PublicAPI]
public class EventStream : EventStream<object>
{
#pragma warning disable CS0618 // Type or member is obsolete
    private readonly ILogger _logger = Log.CreateLogger<EventStream>();
    private readonly PID _pid;
    private readonly ActorSystem _system;
#pragma warning restore CS0618 // Type or member is obsolete

    internal EventStream(ActorSystem system)
    {
        _system = system;
        var props = Props.FromProducer(() => new EventStreamPublisherActor());
        _pid = system.Root.SpawnNamedSystem( props,"$eventstream-actor");
        
        
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var shouldThrottle = Throttle.Create(system.Config.DeadLetterThrottleCount,
            system.Config.DeadLetterThrottleInterval,
            droppedLogs => _logger.LogInformation("[DeadLetter] Throttled {LogCount} logs", droppedLogs)
        );

        Subscribe<DeadLetterEvent>(
            dl =>
            {
                if (system.Config.DeadLetterRequestLogging is false && dl.Sender is not null)
                {
                    return;
                }

                if (!system.Shutdown.IsCancellationRequested && shouldThrottle().IsOpen() &&
                    dl.Message is not IIgnoreDeadLetterLogging)
                {
                    _logger.LogInformation(
                        "[DeadLetter] could not deliver '{MessageType}:{MessagePayload}' to '{Target}' from '{Sender}'",
                        dl.Message.GetMessageTypeName(),
                        dl.Message,
                        dl.Pid,
                        dl.Sender
                    );
                }
            }
        );
    }

    public override void Publish(object msg)
    {
        foreach (var sub in Subscriptions.Values)
        {
            var action = () =>
            {
                sub.Dispatcher.Schedule(
                    () =>
                    {
                        try
                        {
                            sub.Action(msg);
                        }
                        catch (Exception ex)
                        {
                            ex.CheckFailFast();
                            _logger.LogError(0, ex, "Exception has occurred when publishing a message");
                        }

                        return Task.CompletedTask;
                    }
                );
            };

            var runner = new EventStreamRunner(action);
            _system.Root.Send(_pid, runner);
        }
    }
}


public record EventStreamRunner(Action Run);
public class EventStreamPublisherActor : IActor
{
#pragma warning disable CS0618 // Type or member is obsolete
    private readonly ILogger _logger = Log.CreateLogger<EventStreamPublisherActor>();
#pragma warning restore CS0618 // Type or member is obsolete
    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is EventStreamRunner runner)
        {
            try
            { 
                runner.Run();
            }
            catch(Exception ex)
            {
                ex.CheckFailFast();
                _logger.LogError(0, ex, "Exception has occurred when publishing a message");
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
///     Event stream of a specific message type global to an actor system.
/// </summary>
/// <typeparam name="T">Message type</typeparam>
[PublicAPI]
public class EventStream<T>
{
#pragma warning disable CS0618 // Type or member is obsolete
    private readonly ILogger _logger = Log.CreateLogger<EventStream<T>>();
#pragma warning restore CS0618 // Type or member is obsolete

    public ConcurrentDictionary<Guid, EventStreamSubscription<T>> Subscriptions { get; } = new();

    internal EventStream()
    {
    }

    /// <summary>
    ///     Subscribe to messages
    /// </summary>
    /// <param name="action">Synchronous message handler</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe(Action<T> action, IDispatcher? dispatcher = null)
    {
        var sub = new EventStreamSubscription<T>(
            this,
            dispatcher ?? Dispatchers.SynchronousDispatcher,
            x =>
            {
                action(x);

                return Task.CompletedTask;
            }
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to messages and yields the result onto a Channel
    /// </summary>
    /// <param name="channel">a Channel which receives the event</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe(Channel<T> channel, IDispatcher? dispatcher = null)
    {
        var sub = new EventStreamSubscription<T>(
            this,
            dispatcher ?? Dispatchers.SynchronousDispatcher,
            async x => { await channel.Writer.WriteAsync(x).ConfigureAwait(false); }
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to messages with an asynchronous handler
    /// </summary>
    /// <param name="action">Asynchronous message handler</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe(Func<T, Task> action, IDispatcher? dispatcher = null)
    {
        var sub = new EventStreamSubscription<T>(this, dispatcher ?? Dispatchers.SynchronousDispatcher, action);
        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to a message type, which is a derived type from <see cref="T" />
    /// </summary>
    /// <param name="action">Synchronous message handler</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe<TMsg>(Action<TMsg> action, IDispatcher? dispatcher = null)
        where TMsg : T
    {
        var sub = new EventStreamSubscription<T>(
            this,
            dispatcher ?? Dispatchers.SynchronousDispatcher,
            msg =>
            {
                if (msg is TMsg typed)
                {
                    action(typed);
                }

                return Task.CompletedTask;
            }
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to a message type, which is a derived type from <see cref="T" />
    /// </summary>
    /// <param name="predicate">Additional filter upon the typed message</param>
    /// <param name="action">Synchronous message handler</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe<TMsg>(
        Func<TMsg, bool> predicate,
        Action<TMsg> action,
        IDispatcher? dispatcher = null
    ) where TMsg : T
    {
        var sub = new EventStreamSubscription<T>(
            this,
            dispatcher ?? Dispatchers.SynchronousDispatcher,
            msg =>
            {
                if (msg is TMsg typed && predicate(typed))
                {
                    action(typed);
                }

                return Task.CompletedTask;
            }
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to the specified message type, which is a derived type from <see cref="T" />
    /// </summary>
    /// <param name="context">The sender context to send from</param>
    /// <param name="pids">The target PIDs the message will be sent to</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe<TMsg>(ISenderContext context, params PID[] pids) where TMsg : T
    {
        var sub = new EventStreamSubscription<T>(
            this,
            Dispatchers.SynchronousDispatcher,
            msg =>
            {
                if (msg is TMsg)
                {
                    foreach (var pid in pids)
                    {
                        context.Send(pid, msg);
                    }
                }

                return Task.CompletedTask;
            }
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Subscribe to the specified message type, which is a derived type from <see cref="T" />
    /// </summary>
    /// <param name="action">Asynchronous message handler</param>
    /// <param name="dispatcher">Optional: the dispatcher, will use <see cref="Dispatchers.SynchronousDispatcher" /> by default</param>
    /// <returns>A new subscription that can be used to unsubscribe</returns>
    public EventStreamSubscription<T> Subscribe<TMsg>(Func<TMsg, Task> action, IDispatcher? dispatcher = null)
        where TMsg : T
    {
        var sub = new EventStreamSubscription<T>(
            this,
            dispatcher ?? Dispatchers.SynchronousDispatcher,
            msg => msg is TMsg typed ? action(typed) : Task.CompletedTask
        );

        Subscriptions.TryAdd(sub.Id, sub);

        return sub;
    }

    /// <summary>
    ///     Publish a message to the event stream
    /// </summary>
    /// <param name="msg">A message to publish</param>
    public virtual void Publish(T msg)
    {
        foreach (var sub in Subscriptions.Values)
        {
            sub.Dispatcher.Schedule(
                () =>
                {
                    try
                    {
                        sub.Action(msg);
                    }
                    catch (Exception ex)
                    {
                        ex.CheckFailFast();
                        _logger.LogError(0, ex, "Exception has occurred when publishing a message");
                    }

                    return Task.CompletedTask;
                }
            );
        }
    }

    /// <summary>
    ///     Remove a subscription by id
    /// </summary>
    /// <param name="id">Subscription id</param>
    public void Unsubscribe(Guid id) => Subscriptions.TryRemove(id, out _);

    /// <summary>
    ///     Remove a subscription
    /// </summary>
    /// <param name="subscription"> A subscription to remove</param>
    public void Unsubscribe(EventStreamSubscription<T>? subscription)
    {
        if (subscription is not null)
        {
            Unsubscribe(subscription.Id);
        }
    }
}

public class EventStreamSubscription<T>
{
    private readonly EventStream<T> _eventStream;

    public EventStreamSubscription(EventStream<T> eventStream, IDispatcher dispatcher, Func<T, Task> action)
    {
        Id = Guid.NewGuid();
        _eventStream = eventStream;
        Dispatcher = dispatcher;
        Action = action;
    }

    public Guid Id { get; }
    public IDispatcher Dispatcher { get; }
    public Func<T, Task> Action { get; }

    public void Unsubscribe() => _eventStream.Unsubscribe(Id);
}