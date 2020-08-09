using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public class EventProbe
    {
        private EventStream _eventStream;
        private readonly Subscription<object> _subscription;
        private readonly List<EventExpectation> _expectations = new List<EventExpectation>();
        private readonly ConcurrentQueue<object> _events = new ConcurrentQueue<object>();
        private readonly object _lock = new object();

        public EventProbe(EventStream eventStream)
        {
            _eventStream = eventStream;
            _subscription = _eventStream.Subscribe(e =>
                {
                    _events.Enqueue(e);
                    NotifyChanges();
                }
            );
        }

        public Task Expect<T>()
        {
            lock (_lock)
            {
                var expectation = new EventExpectation(@event => @event is T);
                _expectations.Add(expectation);
                NotifyChanges();
                return expectation.Task;
            }
        }

        public Task<object> Expect<T>(Func<T, bool> predicate)
        {
            lock (_lock)
            {
                var expectation = new EventExpectation(@event =>
                    {
                        return @event switch
                        {
                            T e when predicate(e) => true,
                            _ => false
                        };
                    }
                );
                _expectations.Add(expectation);
                NotifyChanges();
                return expectation.Task;
            }
        }

        public void Stop()
        {
            _subscription.Unsubscribe();
        }
        
        //TODO: make lockfree
        private void NotifyChanges()
        {
            lock (_lock)
            {
                if (_expectations.Count == 0)
                {
                    return;
                }
                
                while (_events.TryDequeue(out var @event))
                {
                    Console.WriteLine($"Got event {@event}");
                    foreach (var expectation in _expectations.ToArray())
                    {
                        Console.WriteLine("Evaluating " + expectation);
                        expectation.Evaluate(@event);
                        if (expectation.Done)
                        {
                            Console.WriteLine("Expectation done");
                        }
                    }
                    
                    _expectations.RemoveAll(ex => ex.Done);
                }
            }
        }
    }
}