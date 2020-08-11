using System;
using System.Threading.Tasks;

namespace Proto
{
    internal class EventExpectation<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly TaskCompletionSource<T> _source = new TaskCompletionSource<T>();
        public bool Done { get; private set; }

        public Task<T> Task => _source.Task;

        public EventExpectation(Func<T, bool> predicate)
        {
            _predicate = predicate;
        }

        public void Evaluate(T @event)
        {
            if (Done)
            {
                return;
            }
            
            if (!_predicate(@event))
            {
                return;
            }

            Done = true;
            _source.SetResult(@event);
        }
    }
}