// -----------------------------------------------------------------------
// <copyright file="RouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using Proto.Context;
using Proto.Mailbox;

namespace Proto.Router.Routers
{
    public abstract record RouterConfig
    {
        public abstract void OnStarted(IContext context, RouterState router);

        protected abstract RouterState CreateRouterState();

        public Props Props() => new Props().WithSpawner(SpawnRouterProcess);

        private PID SpawnRouterProcess(ActorSystem system, string name, Props props, PID? parent)
        {
            RouterState? routerState = CreateRouterState();
            AutoResetEvent? wg = new(false);
            Props? p = props.WithProducer(() => new RouterActor(this, routerState, wg));

            IMailbox? mailbox = props.MailboxProducer();
            IDispatcher? dispatcher = props.Dispatcher;
            RouterProcess? process = new(system, routerState, mailbox);
            (var self, bool absent) = system.ProcessRegistry.TryAdd(name, process);

            if (!absent)
            {
                throw new ProcessNameExistException(name, self);
            }

            ActorContext? ctx = ActorContext.Setup(system, p, parent, self, mailbox);
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();
            wg.WaitOne();
            return self;
        }
    }
}
