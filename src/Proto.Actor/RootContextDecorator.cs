using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public abstract class RootContextDecorator : IRootContext
    {
        private readonly IRootContext _context;

        protected RootContextDecorator(IRootContext context)
        {
            _context = context;
        }

        public virtual PID Spawn(Props props) => _context.Spawn(props);

        public virtual PID SpawnNamed(Props props, string name) => _context.SpawnNamed(props, name);

        public virtual PID SpawnPrefix(Props props, string prefix) => _context.SpawnPrefix(props, prefix);

        public virtual void Send(PID target, object message) => _context.Send(target, message);

        public virtual void Request(PID target, object message) => _context.Request(target, message);

        public virtual Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout) =>
            _context.RequestAsync<T>(target, message, timeout);

        public virtual Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
            _context.RequestAsync<T>(target, message, cancellationToken);

        public virtual Task<T> RequestAsync<T>(PID target, object message) => _context.RequestAsync<T>(target, message);

        public virtual MessageHeader Headers => _context.Headers;
        public virtual object Message => _context.Message;
    }
}