using System;
using System.Threading.Tasks;

namespace Proto
{
    public class EventExpectation
    {
        private readonly Func<object, bool> _predicate;
        private readonly TaskCompletionSource<object> _source = new TaskCompletionSource<object>();
        private bool _done;
        public bool Done => _done;

        public Task<object> Task => _source.Task;

        public EventExpectation(Func<object, bool> predicate)
        {
            _predicate = predicate;
        }

        public void Evaluate(object @event)
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