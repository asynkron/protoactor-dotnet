// -----------------------------------------------------------------------
// <copyright file="ActorContextDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Future;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public abstract class ActorContextDecorator : IContext
    {
        private readonly IContext _context;

        protected ActorContextDecorator(IContext context) =>
            _context = context;

        public virtual MessageHeader Headers => _context.Headers;
        public virtual object? Message => _context.Message;
        public virtual PID? Parent => _context.Parent;
        public virtual PID? Self => _context.Self;
        public virtual PID? Sender => _context.Sender;
        public virtual IActor? Actor => _context.Actor;
        public virtual ActorSystem System => _context.System;
        public virtual TimeSpan ReceiveTimeout => _context.ReceiveTimeout;
        public virtual IReadOnlyCollection<PID> Children => _context.Children;
        public CancellationToken CancellationToken => _context.CancellationToken;

        public virtual void Send(PID target, object message) =>
            _context.Send(target, message);

        public virtual void Request(PID target, object message, PID? sender) =>
            _context.Request(target, message, sender);

        public virtual Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => _context.RequestAsync<T>(target, message, cancellationToken);

        public virtual Task Receive(MessageEnvelope envelope) =>
            _context.Receive(envelope);

        public virtual void Respond(object message) =>
            _context.Respond(message);

        public virtual void Stash() =>
            _context.Stash();

        public virtual PID SpawnNamed(Props props, string name) =>
            _context.SpawnNamed(props, name);

        public virtual void Watch(PID pid) =>
            _context.Watch(pid);

        public virtual void Unwatch(PID pid) =>
            _context.Unwatch(pid);

        public virtual void SetReceiveTimeout(TimeSpan duration) =>
            _context.SetReceiveTimeout(duration);

        public virtual void CancelReceiveTimeout() =>
            _context.CancelReceiveTimeout();

        public virtual void Forward(PID target) =>
            _context.Forward(target);

        public virtual void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action) =>
            _context.ReenterAfter(target, action);

        public virtual void ReenterAfter(Task target, Action action) =>
            _context.ReenterAfter(target, action);

        public void Stop(PID pid) => _context.Stop(pid);

        public Task StopAsync(PID pid) => _context.StopAsync(pid);

        public void Poison(PID pid) => _context.Poison(pid);

        public Task PoisonAsync(PID pid) => _context.PoisonAsync(pid);

        public T? Get<T>() => _context.Get<T>();

        public void Set<T, TI>(TI obj) where TI : T => _context.Set<T, TI>(obj);

        public void Remove<T>() => _context.Remove<T>();

        public IFuture GetFuture() => _context.GetFuture();
    }
}