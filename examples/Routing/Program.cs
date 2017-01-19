// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;
using Proto.Routing;

namespace RoutingExample
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
            TestRandomPool();
            TestRandomGroup();

            TestRoundRobinPool();
            TestRoundRobinGroup();

            TestConsistentHashPool();
            TestConsistentHashGroup();

            TestBroadcastPool();
            TestBroadcastGroup();
            Console.ReadLine();
        }

        private static void TestBroadcastGroup()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewBroadcastGroup(
                    MyActorProps,
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps)
                );
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestBroadcastPool()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewBroadcastPool(MyActorProps, 5);
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashGroup()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewConsistentHashGroup(
                    MyActorProps,
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps)
                );
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashPool()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewConsistentHashPool(MyActorProps, 5);
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinGroup()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewRoundRobinGroup(
                    MyActorProps,
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps)
                );
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinPool()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewRoundRobinPool(MyActorProps, 5);
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomGroup()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewRandomGroup(
                    MyActorProps,
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps),
                    Actor.Spawn(MyActorProps)
                );
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomPool()
        {
            for (var i = 0; i < 20; i++)
            {
                var props = Routing.NewRandomPool(MyActorProps, 5);
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }
    }
}