// -----------------------------------------------------------------------
//   <copyright file="Futures.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    internal class FutureActor : IActor
    {
        internal static FutureActor Default = new FutureActor();
        public Task ReceiveAsync(IContext context) => throw new NotSupportedException();
    }

    internal class FutureContext : IContext
    {
        public object Message { get; }
        public MessageHeader Headers { get; }
        public PID Self { get; }
        public PID Sender { get; }
        public IActor Actor => FutureActor.Default;

        public FutureContext(object msg, MessageHeader header, PID self, PID sender)
        {
            Message = msg;
            Headers = header;
            Self = self;
            Sender = sender;
        }

        public PID Parent => throw new NotSupportedException();
        public TimeSpan ReceiveTimeout => throw new NotSupportedException();
        public IReadOnlyCollection<PID> Children => throw new NotSupportedException();
        public void CancelReceiveTimeout() => throw new NotSupportedException();
        public Task ReceiveAsync(object message) => throw new NotSupportedException();
        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action) => throw new NotSupportedException();
        public void ReenterAfter(Task target, Action action) => throw new NotSupportedException();
        public void Request(PID target, object message) => throw new NotSupportedException();
        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout) => throw new NotSupportedException();
        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<T> RequestAsync<T>(PID target, object message) => throw new NotSupportedException();
        public void Respond(object message) => throw new NotSupportedException();
        public void SetReceiveTimeout(TimeSpan duration) => throw new NotSupportedException();
        public PID Spawn(Props props) => throw new NotSupportedException();
        public PID SpawnNamed(Props props, string name) => throw new NotSupportedException();
        public PID SpawnPrefix(Props props, string prefix) => throw new NotSupportedException();
        public void Stash() => throw new NotSupportedException();
        public void Tell(PID target, object message) => throw new NotSupportedException();
        public void Unwatch(PID pid) => throw new NotSupportedException();
        public void Watch(PID pid) => throw new NotSupportedException();
    }

    internal class FutureProcess<T> : Process
    {
        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<T> _tcs;

        internal FutureProcess(TimeSpan timeout) : this(new CancellationTokenSource(timeout)) { }
        internal FutureProcess(CancellationToken cancellationToken) : this(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) { }
        internal FutureProcess() : this(null) { }

        FutureProcess(CancellationTokenSource cts)
        {
            _tcs = new TaskCompletionSource<T>();
            _cts = cts;

            var name = ProcessRegistry.Instance.NextId();
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, this);
            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }
            Pid = pid;

            if (cts != null)
            {
                System.Threading.Tasks.Task.Delay(-1, cts.Token)
                    .ContinueWith(t =>
                    {
                        if (!_tcs.Task.IsCompleted)
                        {
                            _tcs.TrySetException(new TimeoutException("Request didn't receive any Response within the expected time."));
                            Stop(pid);
                        }
                    });
            }

            Task = _tcs.Task;
        }

        public PID Pid { get; }
        public Task<T> Task { get; }

        private Receive _receiveMiddleware;

        internal void SetReceiveMiddleware(Receive middleware) => _receiveMiddleware = middleware;

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            
            if (env.message is T || message == null)
            {
                if (_cts != null && _cts.IsCancellationRequested)
                {
                    Stop(pid);
                    return;
                }

                _tcs.TrySetResult((T)env.message);

                if (_receiveMiddleware != null)
                {
                    _receiveMiddleware(new FutureContext(message, env.headers, Pid, pid));
                }

                Stop(pid);
            }
            else
            {
                throw new InvalidOperationException($"Unexpected message.  Was type {env.message.GetType()} but expected {typeof(T)}");
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                ProcessRegistry.Instance.Remove(Pid);
                _cts?.Dispose();
            }
        }
    }
}