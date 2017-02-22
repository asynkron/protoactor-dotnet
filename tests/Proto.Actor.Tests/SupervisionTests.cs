using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests
    {
        class ParentActor : IActor
        {
            private readonly Props _childProps;

            public ParentActor(Props childProps)
            {
                _childProps = childProps;
            }

            public PID Child { get; set; }

            public Task ReceiveAsync(IContext context)
            {
                if (context.Message is Started)
                    Child = context.Spawn(_childProps);
                if (context.Message is string)
                    Child.Tell(context.Message);
                return Actor.Done;
            }
        }

        class ChildActor : IActor
        {

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string _:
                        throw new Exception();
                }
                return Actor.Done;
            }
        }

        [Fact]
        public void OneForOneStrategy_Should_ResumeChildOnFailure()
        {
            var childMailboxStats = new TestMailboxStatistics();
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 1, TimeSpan.MaxValue);
            var childProps = Actor.FromProducer(() => new ChildActor())
                .WithDispatcher(new TestDispatcher())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Actor.FromProducer(() => new ParentActor(childProps))
                .WithSupervisor(strategy)
                .WithDispatcher(new TestDispatcher());
            var parent = Actor.Spawn(parentProps);

            parent.Tell("hello");

            
        }
    }
}
