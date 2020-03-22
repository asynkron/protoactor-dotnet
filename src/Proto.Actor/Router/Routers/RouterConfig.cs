// -----------------------------------------------------------------------
//   <copyright file="IRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;

namespace Proto.Router.Routers
{
    public abstract class RouterConfig
    {
        public abstract void OnStarted(IContext context, RouterState router);
        public abstract RouterState CreateRouterState();

        public Props Props()
        {
            PID SpawnRouterProcess(ActorSystem system, string name, Props props, PID? parent)
            {
                var routerState = CreateRouterState();
                var wg = new AutoResetEvent(false);
                var p = props.WithProducer(() => new RouterActor(this, routerState, wg));

                var mailbox = props.MailboxProducer();
                var dispatcher = props.Dispatcher;
                var process = new RouterProcess(system, routerState, mailbox);
                var (self, absent) = system.ProcessRegistry.TryAdd(name, process);

                if (!absent)
                {
                    throw new ProcessNameExistException(name, self);
                }

                var ctx = new ActorContext(system, p, parent, self);
                mailbox.RegisterHandlers(ctx, dispatcher);
                mailbox.PostSystemMessage(Started.Instance);
                mailbox.Start();
                wg.WaitOne();
                return self;
            }

            return new Props().WithSpawner(SpawnRouterProcess);
        }
    }
}
