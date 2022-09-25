// -----------------------------------------------------------------------
// <copyright file="RouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using Proto.Context;

namespace Proto.Router.Routers;

public abstract record RouterConfig
{
    public abstract void OnStarted(IContext context, RouterState router);

    protected abstract RouterState CreateRouterState();

    public Props Props() => new Props().WithSpawner(SpawnRouterProcess);

    private PID SpawnRouterProcess(ActorSystem system, string name, Props props, PID? parent,
        Action<IContext>? callback)
    {
        var routerState = CreateRouterState();
        var notifyStarted = new RouterStartNotification();
        var p = props.WithProducer(() => new RouterActor(this, routerState, notifyStarted));

        var mailbox = props.MailboxProducer();
        var dispatcher = props.Dispatcher;
        var process = new RouterProcess(system, routerState, mailbox);
        var (self, absent) = system.ProcessRegistry.TryAdd(name, process);

        if (!absent)
        {
            throw new ProcessNameExistException(name, self);
        }

        var ctx = ActorContext.Setup(system, p, parent, self, mailbox);
        callback?.Invoke(ctx);
        mailbox.RegisterHandlers(ctx, dispatcher);
        mailbox.PostSystemMessage(Started.Instance);
        mailbox.Start();

        var (startSuccess, startException) = notifyStarted.Wait();

        if (!startSuccess)
        {
            system.Root.Stop(self);

            throw new RouterStartFailedException(startException!);
        }

        return self;
    }
}

public class RouterStartNotification
{
    private readonly ManualResetEvent _wg = new(false);
    private Exception? _exception;

    public void NotifyStarted() => _wg.Set();

    public void NotifyFailed(Exception exception)
    {
        _exception = exception;
        _wg.Set();
    }

    public (bool StartSuccess, Exception? Exception) Wait()
    {
        _wg.WaitOne();

        return (_exception is null, _exception);
    }
}

#pragma warning disable RCS1194
public class RouterStartFailedException : Exception
#pragma warning restore RCS1194
{
    public RouterStartFailedException(Exception inner) : base("Router failed to start", inner)
    {
    }
}