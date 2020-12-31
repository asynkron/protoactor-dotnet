// -----------------------------------------------------------------------
// <copyright file="EventProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto
{
    [PublicAPI]
    public static class EventStreamExtensions
    {
        public static EventProbe<T> GetProbe<T>(this EventStream<T> eventStream) => new(eventStream);
    }

    [PublicAPI]
    public class EventProbe<T>
    {
        private readonly ConcurrentQueue<T> _events = new();
        private readonly EventStreamSubscription<T> _eventStreamSubscription;
        private readonly object _lock = new();
        private readonly ILogger _logger = Log.CreateLogger<EventProbe<T>>();
        private EventExpectation<T>? _currentExpectation;

        public EventProbe(EventStream<T> eventStream) => _eventStreamSubscription = eventStream.Subscribe(e => {
                lock (_lock)
                {
                    _events.Enqueue(e);
                    NotifyChanges();
                }
            }
        );

        public Task Expect<TE>() where TE : T
        {
            lock (_lock)
            {
                var expectation = new EventExpectation<T>(@event => @event is TE);
                _currentExpectation = expectation;
                NotifyChanges();
                return expectation.Task;
            }
        }

        public Task<T> Expect<TE>(Func<TE, bool> predicate) where TE : T
        {
            lock (_lock)
            {
                var expectation = new EventExpectation<T>(@event => {
                        return @event switch
                        {
                            TE e when predicate(e) => true,
                            _                      => false
                        };
                    }
                );
                _logger.LogDebug("Setting expectation");
                _currentExpectation = expectation;
                NotifyChanges();
                return expectation.Task;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _currentExpectation = null;
                _eventStreamSubscription.Unsubscribe();
            }
        }

        //TODO: make lockfree
        private void NotifyChanges()
        {
            while (_currentExpectation is not null && _events.TryDequeue(out var @event))
            {
                if (_currentExpectation.Evaluate(@event))
                {
                    _logger.LogDebug("Got expected event {@event} ", @event);
                    _currentExpectation = null;
                    return;
                }

                _logger.LogDebug("Got unexpected {@event}, ignoring", @event);
            }
        }
    }
}