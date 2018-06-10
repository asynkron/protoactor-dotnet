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
            var context = new RootContext();
            var props = Router.NewBroadcastGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            for (var i = 0; i < 10; i++)
            {
                var pid = context.Spawn(props);
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestBroadcastPool()
        {
            var context = new RootContext();
            var props = Router.NewBroadcastPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashGroup()
        {
            var context = new RootContext();
            var props = Router.NewConsistentHashGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestConsistentHashPool()
        {
            var context = new RootContext();
            var props = Router.NewConsistentHashPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinGroup()
        {
            var context = new RootContext();
            var props = Router.NewRoundRobinGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRoundRobinPool()
        {
            var context = new RootContext();
            var props = Router.NewRoundRobinPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomGroup()
        {
            var context = new RootContext();
            var props = Router.NewRandomGroup(
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps),
                context.Spawn(MyActorProps)
            );
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }

        private static void TestRandomPool()
        {
            var context = new RootContext();
            var props = Router.NewRandomPool(MyActorProps, 5);
            var pid = context.Spawn(props);
            for (var i = 0; i < 10; i++)
            {
                context.Send(pid, new Message {Text = $"{i % 4}"});
            }
        }
    }
}