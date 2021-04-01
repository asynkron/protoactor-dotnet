// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
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

        public string HashBy() => Text;

        public override string ToString() => Text;
    }

    internal class MyActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Message msg)
            {
                Console.WriteLine($"Actor {context.Self.Id} got message '{msg.Text}'.");
            }

            return Task.CompletedTask;
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
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewBroadcastGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );

            for (int i = 0; i < 10; i++)
            {
                PID pid = context.Spawn(props);
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestBroadcastPool()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewBroadcastPool(MyActorProps, 5);
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashGroup()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewConsistentHashGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashPool()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewConsistentHashPool(MyActorProps, 5);
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinGroup()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewRoundRobinGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinPool()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewRoundRobinPool(MyActorProps, 5);
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomGroup()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewRandomGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomPool()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);
            Props props = context.NewRandomPool(MyActorProps, 5);
            PID pid = context.Spawn(props);

            for (int i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }
    }
}
