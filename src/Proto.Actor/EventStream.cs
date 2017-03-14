// -----------------------------------------------------------------------
//  <copyright file="EventStream.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Proto
{
    public class EventStream : EventStream<object>
    {
        public static readonly EventStream Instance = new EventStream();

        private readonly ILogger logger;

        public EventStream()
        {
            logger = Log.CreateLogger<EventStream>();

            Subscribe(msg =>
            {
                if (msg is DeadLetterEvent letter)
                {
                    logger.LogInformation("[DeadLetter] '{0}' got '{1}:{2}' from '{3}'", letter.Pid.ToShortString(),
                        letter.Message.GetType().Name, letter.Message, letter.Sender?.ToShortString());
                }
            });
        }
    }
    public class EventStream<T>
    {
        private readonly ConcurrentDictionary<Guid, Action<T>> _subscriptions =
            new ConcurrentDictionary<Guid, Action<T>>();

        public Subscription<T> Subscribe(Action<T> action)
        {
            var sub = Guid.NewGuid();
            _subscriptions.TryAdd(sub, action);
            return new Subscription<T>(sub, this);
        }

        public void Publish(T msg)
        {
            foreach (var sub in _subscriptions)
            {
                sub.Value(msg);
            }
        }

        internal void Unsubscribe(Guid id)
        {
            _subscriptions.TryRemove(id, out var _);
        }
    }

    public class Subscription<T>
    {
        private Guid _id;
        private EventStream<T> _eventStream;

        public Subscription(Guid sub, EventStream<T> eventStream)
        {
            _id = sub;
            _eventStream = eventStream;
        }

        public void Unsubscribe()
        {
            _eventStream.Unsubscribe(_id);
        }
    }
}