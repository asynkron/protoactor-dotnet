using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class DisposableActorTests
    {
        [Fact]
        public void WhenActorStopped_DisposeIsCalled()
        {
            var disposeCalled = false;
            var props = Actor.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => new TestMailbox());
            var pid = Actor.Spawn(props);
            pid.Stop();
            Assert.True(disposeCalled);
        }

        [Fact]
        public void WhenActorRestarted_DisposeIsCalled()
        {
            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var disposeCalled = false;
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 0, null);
            var childProps = Actor.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats))
                .WithChildSupervisorStrategy(strategy);
            var props = Actor.FromProducer(() => new SupervisingActor(childProps))
                .WithMailbox(() => new TestMailbox())
                .WithChildSupervisorStrategy(strategy);
            var parentPID = Actor.Spawn(props);
            parentPID.Tell("crash");
            childMailboxStats.Reset.Wait(1000);
            Assert.True(disposeCalled);
        }

        [Fact]
        public void WhenActorResumed_DisposeIsNotCalled()
        {
            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var disposeCalled = false;
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 0, null);
            var childProps = Actor.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats))
                .WithChildSupervisorStrategy(strategy);
            var props = Actor.FromProducer(() => new SupervisingActor(childProps))
                .WithMailbox(() => new TestMailbox())
                .WithChildSupervisorStrategy(strategy);
            var parentPID = Actor.Spawn(props);
            parentPID.Tell("crash");
            childMailboxStats.Reset.Wait(1000);
            Assert.False(disposeCalled);
        }

        [Fact]
        public void WhenActorWithChildrenStopped_DisposeIsCalledInEachChild()
        {
            bool child1Disposed = false;
            bool child2Disposed = false;
            var child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            var child1Props = Actor.FromProducer(() => new DisposableActor(() => child1Disposed = true))
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Actor.FromProducer(() => new DisposableActor(() => child2Disposed = true))
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Actor.FromProducer(() => new ParentWithMultipleChildrenActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = Actor.Spawn(parentProps);

            parent.Tell("crash");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.True(child1Disposed);
            Assert.True(child2Disposed);
        }
        
        private class SupervisingActor : IActor
        {
            private readonly Props _childProps;
            private PID _childPID;

            public SupervisingActor(Props childProps)
            {
                _childProps = childProps;
            }

            public Task ReceiveAsync(IContext context)
            {
                if (context.Message is Started)
                    _childPID = context.Spawn(_childProps);
                if (context.Message is string)
                    _childPID.Tell(context.Message);
                return Actor.Done;
            }
        }

        private class DisposableActor : IActor, IDisposable
        {
            private readonly Action _onDispose;

            public DisposableActor(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string msg:
                        throw new Exception();
                }
                return Actor.Done;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }

        private class ParentWithMultipleChildrenActor : IActor
        {
            private readonly Props _child1Props;
            private readonly Props _child2Props;

            public ParentWithMultipleChildrenActor(Props child1Props, Props child2Props)
            {
                _child1Props = child1Props;
                _child2Props = child2Props;
            }

            public PID Child1 { get; set; }
            public PID Child2 { get; set; }

            public Task ReceiveAsync(IContext context)
            {
                if (context.Message is Started)
                {
                    Child1 = context.Spawn(_child1Props);
                    Child2 = context.Spawn(_child2Props);
                }

                if (context.Message is string)
                {
                    // only tell one child
                    Child1.Tell(context.Message);
                }

                return Actor.Done;
            }
        }

        
    }
}
