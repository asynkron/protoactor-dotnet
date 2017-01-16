// -----------------------------------------------------------------------
//  <copyright file="Actor.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto
{
    public delegate Task Receive(IContext context);

    public class EmptyActor : IActor
    {
        private readonly Receive _receive;

        public EmptyActor(Receive receive)
        {
            _receive = receive;
        }

        public Task ReceiveAsync(IContext context)
        {
            return _receive(context);
        }
    }

    public static class Actor
    {
        public static readonly Task Done = Task.FromResult(0);

        public static Props FromProducer(Func<IActor> producer)
        {
            return new Props().WithProducer(producer);
        }

        public static Props FromFunc(Receive receive)
        {
            return FromProducer(() => new EmptyActor(receive));
        }

        public static Props FromGroupRouter(IGroupRouterConfig routerConfig)
        {
            return new Props().WithRouter(routerConfig);
        }

        public static PID Spawn(Props props)
        {
            var name = ProcessRegistry.Instance.GetAutoId();
            return InternalSpawn(props, name, null);
        }

        public static PID SpawnNamed(Props props, string name)
        {
            return InternalSpawn(props, name, null);
        }


        private static PID SpawnRouter(string name, Props props, PID parent)
        {
            var routeeProps = props.WithRouter(null);
            var config = props.RouterConfig;
            var routerState = config.CreateRouterState();

            var routerProps = FromProducer(() => new RouterActor(routeeProps, config, routerState));
            var routerId = ProcessRegistry.Instance.GetAutoId();
            var router = InternalSpawn(routerProps, routerId, parent);

            var reff = new RouterActorRef(router, routerState);
            var res = ProcessRegistry.Instance.TryAdd(name, reff);
            var pid = res.Item1;
            return pid;
        }

        internal static PID InternalSpawn(Props props, string name, PID parent)
        {
            if (props.RouterConfig != null)
            {
                return SpawnRouter(name, props, parent);
            }

            var ctx = new Context(props, parent);
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var reff = new LocalActorRef(mailbox);
            var res = ProcessRegistry.Instance.TryAdd(name, reff);
            var pid = res.Item1;
            var @new = res.Item2;
            if (!@new)
            {
                return pid;
            }

            mailbox.RegisterHandlers(ctx, dispatcher);
            ctx.Self = pid;
            //this is on purpose, Started is synchronous to its parent
            ctx.InvokeUserMessageAsync(Started.Instance).Wait();
            return pid;
        }
    }

    public interface IActor
    {
        Task ReceiveAsync(IContext context);
    }
}