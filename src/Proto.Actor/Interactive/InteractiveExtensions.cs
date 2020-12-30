// -----------------------------------------------------------------------
// <copyright file="InteractiveExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Mailbox;
using Proto.Router;

namespace Proto.Interactive
{
    [PublicAPI]
    public static class InteractiveExtensions
    {
        public static async Task ParallelForEach<T>(
            this IEnumerable<T> self,
            Func<T, Task> body,
            int concurrencyLevel = 10
        )
        {
            var (s, routees, router) = Start(body, concurrencyLevel);

            foreach (var msg in self)
            {
                s.Root.Send(router, msg);
            }

            await Shutdown(routees, s, router);
        }

        public static async Task ParallelForEach<T>(
            this IAsyncEnumerable<T> self,
            Func<T, Task> body,
            int concurrencyLevel = 10
        )
        {
            var (s, routees, router) = Start(body, concurrencyLevel);

            await foreach (var msg in self)
            {
                s.Root.Send(router, msg);
            }

            await Shutdown(routees, s, router);
        }

        public static Task ParallelForEach<T>(
            this IEnumerable<T> self,
            Action<T> body,
            int concurrencyLevel = 10
        ) =>
            self.ParallelForEach(x => {
                    body(x);
                    return Task.CompletedTask;
                }, concurrencyLevel
            );

        private static async Task Shutdown(PID[] routees, ActorSystem s, PID router)
        {
            foreach (var routee in routees)
            {
                await s.Root.PoisonAsync(routee);
            }

            await s.Root.StopAsync(router);
        }

        private static (ActorSystem, PID[], PID) Start<T>(Func<T, Task> body, int concurrencyLevel)
        {
            var s = new ActorSystem();
            var props = Props.FromFunc(async ctx => {
                    if (ctx.Message is T msg) await body(msg);
                }
            ).WithMailbox(() => new DefaultMailbox(new UnboundedMailboxQueue(), new BoundedMailboxQueue(10)));

            var routees = Enumerable
                .Range(0, concurrencyLevel)
                .Select(_ => s.Root.Spawn(props))
                .ToArray();

            var routerProps = s.Root.NewRoundRobinGroup(routees);
            var router = s.Root.Spawn(routerProps);
            return (s, routees, router);
        }
    }
}