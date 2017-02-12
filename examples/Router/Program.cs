// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
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
            TestBroadcastPool();
            //TestBroadcastGroup();

            //TestRandomPool();
            //TestRandomGroup();

            //TestRoundRobinPool();
            //TestRoundRobinGroup();

            //TestConsistentHashPool();
            //TestConsistentHashGroup();

            Console.ReadLine();
        }

        private static void TestBroadcastGroup()
        {
            var props = Router.NewBroadcastGroup(
                MyActorProps,
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps)
            );
            for (var i = 0; i < 10; i++)
            {
                var pid = Actor.Spawn(props);
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestBroadcastPool()
        {
            var props = Router.NewBroadcastPool(MyActorProps, 5);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashGroup()
        {
            var props = Router.NewConsistentHashGroup(
                MyActorProps,
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps)
            );
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashPool()
        {
            var props = Router.NewConsistentHashPool(MyActorProps, 5);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinGroup()
        {
            var props = Router.NewRoundRobinGroup(
                MyActorProps,
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps)
            );
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinPool()
        {
            var props = Router.NewRoundRobinPool(MyActorProps, 5);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomGroup()
        {
            var props = Router.NewRandomGroup(
                MyActorProps,
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps),
                Actor.Spawn(MyActorProps)
            );
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomPool()
        {
            var props = Router.NewRandomPool(MyActorProps, 5);
            var pid = Actor.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                pid.Tell(new Message {Text = $"{i % 4}"});
            }
        }
    }
}