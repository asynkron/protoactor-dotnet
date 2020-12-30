// -----------------------------------------------------------------------
// <copyright file="RouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using Proto.Context;

namespace Proto.Router.Routers
{
    public abstract record RouterConfig
    {
        public abstract void OnStarted(IContext context, RouterState router);

        protected abstract RouterState CreateRouterState();

        public Props Props() => new Props().WithSpawner(SpawnRouterProcess);

        private PID SpawnRouterProcess(ActorSystem system, string name, Props props, PID? parent)
        {
            var routerState = CreateRouterState();
            var wg = new AutoResetEvent(false);
            var p = props.WithProducer(() => new RouterActor(this, routerState, wg));

            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var process = new RouterProcess(system, routerState, mailbox);
            var (self, absent) = system.ProcessRegistry.TryAdd(name, process);

            if (!absent) throw new ProcessNameExistException(name, self);

            var ctx = ActorContext.Setup(system, p, parent, self);
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();
            wg.WaitOne();
            return self;
        }
    }
}