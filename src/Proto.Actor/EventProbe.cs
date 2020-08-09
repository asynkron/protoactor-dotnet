using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto
{
    public class EventProbe
    {
        private EventStream _eventStream;
        private readonly Subscription<object> _subscription;
        private readonly List<EventExpectation> _expectations = new List<EventExpectation>();

        public EventProbe(EventStream eventStream)
        {
            _eventStream = eventStream;
            _subscription = _eventStream.Subscribe(NotifyChanges);
        }
        
        public Task Expect<T>()
        {
            var expectation = new EventExpectation(@event => @event is T);
            _expectations.Add(expectation);
            return expectation.Task;
        }

        public Task<object> Expect<T>(Func<T, bool> predicate)
        {
            var expectation = new EventExpectation(@event =>
            {
                return @event switch
                {
                    T e when predicate(e) => true,
                    _ => false
                };
            });
            _expectations.Add(expectation);
            return expectation.Task;
        }

        public void Stop()
        {
            _subscription.Unsubscribe();
        }

        private void NotifyChanges(object @event)
        {
            Console.WriteLine($"Got event {@event}");
            foreach (var expectation in _expectations)
            {
                expectation.Evaluate(@event);
            }
        }
    }
}