using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    /// <summary>
    /// Note in these tests we dynamically find the routees that processed messages due to the way the consistent hashing works. 
    /// The generated hash might differ per environment / runtime. There is also no attempt made to assert on which routee
    /// receives which message - only that messages are distributed amongst routees
    /// </summary>
    public class ConsistentHashGroupTests
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

        private static readonly Props MyActorProps = Actor.FromProducer(() => new MyTestActor())
            .WithMailbox(() => new TestMailbox());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(250);

        [Fact]
        public async void ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            router.Tell(new Message("test"));
            router.Tell(new Message("test"));
            router.Tell(new Message("test"));
            
            await AssertOnOneRouteeReceivingMessagesOnly(3, routee1, routee2, routee3);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessagesWithDifferentHashesGoToDifferentRoutees()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            foreach (var character in Alphabet)
            {
                router.Tell(new Message(character.ToString()));
            }
            
            await AssertOnRouteesRecevingMessages(routee1, routee2, routee3);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee_EvenWhenNewRouteeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            router.Tell(new Message("test"));
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee{PID = routee4});
            router.Tell(new Message("test"));
            
            await AssertOnOneRouteeReceivingMessagesOnly(2, routee1, routee2, routee3, routee4);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            router.Tell(new RouterRemoveRoutee { PID = routee1 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, _, _) = CreateRouterWith3Routees();
            
            router.Tell(new RouterRemoveRoutee { PID = routee1 });
            foreach (var character in Alphabet)
            {
                router.Tell(new Message(character.ToString()));
            }

            Assert.Equal(0, await routee1.RequestAsync<int>("received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });
            foreach (var character in Alphabet)
            {
                router.Tell(new Message(character.ToString()));
            }
            await AssertOnRouteesRecevingMessages(routee1, routee2, routee3, routee4);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessageIsReassignedWhenRouteeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            router.Tell(new Message("test"));
            // one routee handles "test"
            var receiver = await AssertOnOneRouteeReceivingMessagesOnly(1, routee1, routee2, routee3);
            // remove receiver
            router.Tell(new RouterRemoveRoutee { PID = receiver });
            // some other routee should now handle "test"
            router.Tell(new Message("test"));

            var routees = new List<PID> {routee1, routee2, routee3}.Where(r => r != receiver).ToArray();
            Assert.Equal(2, routees.Length);
            await AssertOnOneRouteeReceivingMessagesOnly(1, routees);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            router.Tell(new RouterBroadcastMessage { Message = new Message("hello") });

            Assert.Equal(1, await routee1.RequestAsync<int>("received?", _timeout));
            Assert.Equal(1, await routee2.RequestAsync<int>("received?", _timeout));
            Assert.Equal(1, await routee3.RequestAsync<int>("received?", _timeout));
        }

        private async Task AssertOnRouteesRecevingMessages(params PID[] routees)
        {
            foreach (var routee in routees)
            {
                Assert.True(await routee.RequestAsync<int>("received?", _timeout) > 0);
            }
        }

        private async Task<PID> AssertOnOneRouteeReceivingMessagesOnly(int messagesCount, params PID[] routees)
        {
            PID receiver = null;
            foreach (var r in routees)
            {
                var messagesReceived = await r.RequestAsync<int>("received?", _timeout);
                if (messagesReceived != messagesCount) continue;
                if (receiver != null)
                {
                    throw new Exception("More than one routee received messages");
                }
                receiver = r;
            }
            
            Assert.NotNull(receiver);
            // Assert no other routees received any messages
            foreach (var r in routees.Where(r => r != receiver))
            {
                Assert.Equal(0, await r.RequestAsync<int>("received?", _timeout));
            }
            return receiver;
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees()
        {
            var routee1 = Actor.Spawn(MyActorProps);
            var routee2 = Actor.Spawn(MyActorProps);
            var routee3 = Actor.Spawn(MyActorProps);

            var props = Router.NewConsistentHashGroup(MyActorProps, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = Actor.Spawn(props);
            return (router, routee1, routee2, routee3);
        }

        internal class Message : IHashable
        {
            private readonly string _value;

            public Message(string value)
            {
                _value = value;
            }
            public string HashBy()
            {
                return _value;
            }

            public override string ToString()
            {
                return _value;
            }
        }

        internal class MyTestActor : IActor
        {
            private readonly List<string> _receivedMessages = new List<string>();
            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string msg when msg == "received?":
                        context.Sender.Tell(_receivedMessages.Count);
                        break;
                    case Message msg:
                        _receivedMessages.Add(msg.ToString());
                        break;
                }
                return Actor.Done;
            }
        }
    }
}
