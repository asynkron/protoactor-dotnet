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
            PID SpawnRouterProcess(string name, Props props, PID parent)
            {
                var routerState = CreateRouterState();
                var wg = new AutoResetEvent(false);
                var p = props.WithProducer(() => new RouterActor(this, routerState, wg));
   
                var ctx = new ActorContext(p, parent);
                var mailbox = props.MailboxProducer();
                var dispatcher = props.Dispatcher;
                var process = new RouterProcess(routerState, mailbox);
                var (self, absent) = ProcessRegistry.Instance.TryAdd(name, process);
                if (!absent)
                {
                    throw new ProcessNameExistException(name, self);
                }
                ctx.Self = self;
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