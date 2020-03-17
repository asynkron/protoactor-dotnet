// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;
using Proto.Router;

namespace RouterExample
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
            if (context.Message is Message msg)
            {
                Console.WriteLine($"Actor {context.Self.Id} got message '{msg.Text}'.");
            }
            return Actor.Done;
        }
    }

    internal class Program
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyActor());

        private static void Main()
        {
            TestBroadcastPool();
            TestBroadcastGroup();

            TestRandomPool();
            TestRandomGroup();

            TestRoundRobinPool();
            TestRoundRobinGroup();

            TestConsistentHashPool();
            TestConsistentHashGroup();

            Console.ReadLine();
        }

        private static void TestBroadcastGroup()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewBroadcastGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            for (var i = 0; i < 10; i++)
            {
                var pid = context.Spawn(props);
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestBroadcastPool()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewBroadcastPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestConsistentHashGroup()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewConsistentHashGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestConsistentHashPool()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewConsistentHashPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestRoundRobinGroup()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewRoundRobinGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestRoundRobinPool()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewRoundRobinPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestRandomGroup()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewRandomGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }

        private static void TestRandomPool()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = context.NewRandomPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message { Text = $"{i % 4}" });
            }
        }
    }
}