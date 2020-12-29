// -----------------------------------------------------------------------
// <copyright file="Context.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Context
{
    public abstract class DecoratorContext : IContext
        {
            private readonly IContext _context;

            public IContext InnerContext => _context;

            protected DecoratorContext(IContext context)
            {
                _context = context;
            }

            public virtual PID? Parent => _context.Parent;
            public virtual PID? Self => _context.Self;
            public virtual PID? Sender => _context.Sender;
            public virtual IActor? Actor => _context.Actor;
            public virtual ActorSystem System => _context.System;

            public virtual async Task Receive(MessageEnvelope envelope) => await _context.Receive(envelope);

            public virtual MessageHeader Headers => _context.Headers;
            public virtual object? Message => _context.Message;

            public virtual void Send(PID target, object message) => _context.Send(target, message);

            public virtual void Request(PID target, object message) => _context.Request(target, message);

            public virtual void Request(PID target, object message, PID? sender) => _context.Request(target, message, sender);

            public virtual Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout) => _context.RequestAsync<T>(target, message, timeout);

            public virtual Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
                => _context.RequestAsync<T>(target, message, cancellationToken);

            public virtual Task<T> RequestAsync<T>(PID target, object message) => _context.RequestAsync<T>(target, message);

            public virtual PID Spawn(Props props) => _context.Spawn(props);

            public virtual PID SpawnNamed(Props props, string name) => _context.SpawnNamed(props, name);

            public virtual PID SpawnPrefix(Props props, string prefix) => _context.SpawnPrefix(props, prefix);

            public virtual void Stop(PID pid) => _context.Stop(pid);

            public virtual Task StopAsync(PID pid) => _context.StopAsync(pid);

            public virtual void Poison(PID pid) => _context.Poison(pid);

            public virtual Task PoisonAsync(PID pid) => _context.PoisonAsync(pid);

            public virtual CancellationToken CancellationToken => _context.CancellationToken;
            public virtual TimeSpan ReceiveTimeout => _context.ReceiveTimeout;
            public virtual IReadOnlyCollection<PID> Children => _context.Children;

            public virtual void Respond(object message) => _context.Respond(message);

            public virtual void Stash() => _context.Stash();

            public virtual void Watch(PID pid) => _context.Watch(pid);

            public virtual void Unwatch(PID pid) => _context.Unwatch(pid);

            public virtual void SetReceiveTimeout(TimeSpan duration) => _context.SetReceiveTimeout(duration);

            public virtual void CancelReceiveTimeout() => _context.CancelReceiveTimeout();

            public virtual void Forward(PID target) => _context.Forward(target);

            public virtual void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action) => _context.ReenterAfter(target, action);

            public virtual void ReenterAfter(Task target, Action action) => _context.ReenterAfter(target, action);
        }
}