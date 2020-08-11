using System;
using System.Threading.Tasks;

namespace Proto
{
    internal class EventExpectation<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly TaskCompletionSource<T> _source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _source.Task;

        public EventExpectation(Func<T, bool> predicate)
        {
            _predicate = predicate;
        }

        public bool Evaluate(T @event)
        {
            if (!_predicate(@event))
            {
                return false;
            }
            
            _source.SetResult(@event);
            return true;
        }
    }
}