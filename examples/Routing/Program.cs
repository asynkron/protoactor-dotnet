// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

namespace SpawnBenchmark
{
    internal class Message : IHashable
    {
        public string Text;

        public string HashBy()
        {
            return Text;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    internal class MyActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message as Message;
            if (msg != null)
            {
                Console.WriteLine($"Actor {context.Self.Id} got message '{msg.Text}'.");
            }
            return Actor.Done;
        }
    }

    internal class Program
    {
        private static readonly Props MyActorProps = Actor.FromProducer(() => new MyActor());

        private static void Main()
        {
            //TestPoolRouter(new RandomPoolRouter(5), 10);
            //TestGroupRouter(new RandomGroupRouter(
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps)
            //), 10);

            //TestPoolRouter(new RoundRobinPoolRouter(5), 10);
            TestGroupRouter(new RoundRobinGroupRouter(
                Actor.Spawn(MyActorProps)
                //Actor.Spawn(MyActorProps),
                //Actor.Spawn(MyActorProps),
                //Actor.Spawn(MyActorProps)
            ), 10);

            //TestPoolRouter(new ConsistentHashPoolRouter(5), 20, 8);
            //TestGroupRouter(new ConsistentHashGroupRouter(
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps)
            //), 20, 8);

            //TestPoolRouter(new BroadcastPoolRouter(5), 5);
            //TestGroupRouter(new BroadcastGroupRouter(
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps),
            //    Actor.Spawn(MyActorProps)
            //), 5);
            Console.ReadLine();
        }

        private static void TestGroupRouter(IGroupRouterConfig groupRouter, int count, int? unique = null)
        {
            var props = Actor.FromGroupRouter(groupRouter);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < count; i++)
            {
                pid.Tell(new Message {Text = $"{i % (unique ?? count)} {groupRouter.GetType().Name}"});
            }
        }

        private static void TestPoolRouter(IPoolRouterConfig poolRouter, int count, int? unique = null)
        {
            var props = MyActorProps.WithPoolRouter(poolRouter);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < count; i++)
            {
                pid.Tell(new Message {Text = $"{i % (unique ?? count)} {poolRouter.GetType().Name}"});
            }
        }
    }
}