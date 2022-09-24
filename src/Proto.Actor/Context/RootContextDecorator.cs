﻿// -----------------------------------------------------------------------
// <copyright file="RootContextDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Future;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     A base class for decorators that decorate (extend) a <see cref="RootContext" />.
/// </summary>
[PublicAPI]
public abstract class RootContextDecorator : IRootContext
{
    private readonly IRootContext _context;

    protected RootContextDecorator(IRootContext context)
    {
        _context = context;
    }

    public virtual PID SpawnNamed(Props props, string name, Action<IContext>? callback = null)
    {
        return _context.SpawnNamed(props, name, callback);
    }

    public virtual void Send(PID target, object message)
    {
        _context.Send(target, message);
    }

    public virtual void Request(PID target, object message, PID? sender)
    {
        _context.Request(target, message, sender);
    }

    public virtual Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
    {
        return _context.RequestAsync<T>(target, message, cancellationToken);
    }

    public virtual MessageHeader Headers => _context.Headers;

    public virtual object? Message => _context.Message;

    public virtual void Stop(PID pid)
    {
        _context.Stop(pid);
    }

    public virtual Task StopAsync(PID pid)
    {
        return _context.StopAsync(pid);
    }

    public virtual void Poison(PID pid)
    {
        _context.Poison(pid);
    }

    public virtual Task PoisonAsync(PID pid)
    {
        return _context.PoisonAsync(pid);
    }

    public IRootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
    {
        return WithInnerContext(_context.WithSenderMiddleware(middleware));
    }

    public virtual PID? Parent => null;
    public virtual PID? Self => null;
    public virtual PID? Sender => null;
    public virtual IActor? Actor => null;
    public virtual ActorSystem System => _context.System;

    public virtual T? Get<T>()
    {
        return _context.Get<T>();
    }

    public virtual void Set<T, TI>(TI obj) where TI : T
    {
        _context.Set(obj);
    }

    public virtual void Remove<T>()
    {
        _context.Remove<T>();
    }

    public IFuture GetFuture()
    {
        return _context.GetFuture();
    }

    protected abstract IRootContext WithInnerContext(IRootContext context);

    public virtual void Request(PID target, object message)
    {
        _context.Request(target, message);
    }
}