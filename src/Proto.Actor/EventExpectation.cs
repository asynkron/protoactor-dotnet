using System;
using System.Threading.Tasks;

namespace Proto
{
    public class EventExpectation<T>
    {
        private readonly Func<object, bool> _predicate;
        private readonly TaskCompletionSource<T> _source = new TaskCompletionSource<T>();
        private bool _done;
        public bool Done => _done;

        public Task<T> Task => _source.Task;

        public EventExpectation(Func<object, bool> predicate)
        {
            _predicate = predicate;
        }

        public void Evaluate(T @event)
        {
            if (_done)
            {
                return;
            }
            
            if (!_predicate(@event))
            {
                return;
            }

            _done = true;
            _source.SetResult(@event);
        }
    }
}