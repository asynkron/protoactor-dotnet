// -----------------------------------------------------------------------
//   <copyright file="EventStream.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

namespace Proto
{
    public class EventStream : EventStream<object>
    {
        public static readonly EventStream Instance = new EventStream();

        private readonly ILogger _logger = Log.CreateLogger<EventStream>();

        internal EventStream()
        {
            Subscribe(msg =>
            {
                if (msg is DeadLetterEvent letter)
                {
                    _logger.LogInformation("[DeadLetter] '{0}' got '{1}:{2}' from '{3}'", letter.Pid.ToShortString(),
                        letter.Message.GetType().Name, letter.Message, letter.Sender?.ToShortString());
                }
            });
        }
    }
    public class EventStream<T>
    {
        private readonly ILogger _logger = Log.CreateLogger<EventStream<T>>();

        private readonly ConcurrentDictionary<Guid, Subscription<T>> _subscriptions =
            new ConcurrentDictionary<Guid, Subscription<T>>();

        internal EventStream()
        {
        }

        public Subscription<T> Subscribe(Action<T> action, IDispatcher dispatcher = null)
        {
            var sub = new Subscription<T>(this, dispatcher ?? Dispatchers.SynchronousDispatcher, x =>
            {
                action(x);
                return Actor.Done;
            });
            _subscriptions.TryAdd(sub.Id, sub);
            return sub;
        }

        public Subscription<T> Subscribe(Func<T, Task> action, IDispatcher dispatcher = null)
        {
            var sub = new Subscription<T>(this, dispatcher ?? Dispatchers.SynchronousDispatcher, action);
            _subscriptions.TryAdd(sub.Id, sub);
            return sub;
        }

        public Subscription<T> Subscribe<TMsg>(Action<TMsg> action, IDispatcher dispatcher = null) where TMsg : T
        {
            var sub = new Subscription<T>(this, dispatcher ?? Dispatchers.SynchronousDispatcher, msg =>
            {
                if (msg is TMsg typed)
                {
                    action(typed);
                }
                return Actor.Done;
            });

            _subscriptions.TryAdd(sub.Id, sub);
            return sub;
        }

        public void Publish(T msg)
        {
            foreach (var sub in _subscriptions)
            {
                sub.Value.Dispatcher.Schedule(() =>
                {
                    try
                    {
                        sub.Value.Action(msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(0, ex, "Exception has occurred when publishing a message.");
                    }
                    return Actor.Done;
                });
            }
        }

        public void Unsubscribe(Guid id)
        {
            _subscriptions.TryRemove(id, out var _);
        }
    }

    public class Subscription<T>
    {
        private readonly EventStream<T> _eventStream;

        public Subscription(EventStream<T> eventStream, IDispatcher dispatcher, Func<T, Task> action)
        {
            Id = Guid.NewGuid();
            _eventStream = eventStream;
            Dispatcher = dispatcher;
            Action = action;
        }

        public Guid Id { get; }
        public IDispatcher Dispatcher { get; }
        public Func<T, Task> Action { get; }

        public void Unsubscribe()
        {
            _eventStream.Unsubscribe(Id);
        }
    }
}