using System;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class DisposableActorTests
    {
        private static readonly RootContext Context = new RootContext();
        
        private class SupervisingActor : IActor
        {
            private readonly Props _childProps;
            private PID _childPid;

            public SupervisingActor(Props childProps)
            {
                _childProps = childProps;
            }

            public Task ReceiveAsync(IContext context)
            {
                if (context.Message is Started)
                    _childPid = context.Spawn(_childProps);
                if (context.Message is string)
                    context.Send(_childPid, context.Message);
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
                    case string _:
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

            private PID Child1 { get; set; }
            private PID Child2 { get; set; }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        Child1 = context.Spawn(_child1Props);
                        Child2 = context.Spawn(_child2Props);
                        break;
                    case string _:
                        context.Send(Child1, context.Message);
                        break;
                }

                return Actor.Done;
            }
        }

        [Fact]
        public void WhenActorRestarted_DisposeIsCalled()
        {
            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var disposeCalled = false;
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 0, null);
            var childProps = Props.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats))
                .WithChildSupervisorStrategy(strategy);
            var props = Props.FromProducer(() => new SupervisingActor(childProps))
                .WithMailbox(() => new TestMailbox())
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(props);
            Context.Send(parent, "crash");
            childMailboxStats.Reset.Wait(1000);
            Assert.True(disposeCalled);
        }

        [Fact]
        public void WhenActorResumed_DisposeIsNotCalled()
        {
            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var disposeCalled = false;
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 0, null);
            var childProps = Props.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats))
                .WithChildSupervisorStrategy(strategy);
            var props = Props.FromProducer(() => new SupervisingActor(childProps))
                .WithMailbox(() => new TestMailbox())
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(props);
            Context.Send(parent, "crash");
            childMailboxStats.Reset.Wait(1000);
            Assert.False(disposeCalled);
        }

        [Fact]
        public async void WhenActorStopped_DisposeIsCalled()
        {
            var disposeCalled = false;
            var props = Props.FromProducer(() => new DisposableActor(() => disposeCalled = true))
                .WithMailbox(() => new TestMailbox());
            var pid = Context.Spawn(props);
            await pid.StopAsync();
            Assert.True(disposeCalled);
        }

        [Fact]
        public void WhenActorWithChildrenStopped_DisposeIsCalledInEachChild()
        {
            var child1Disposed = false;
            var child2Disposed = false;
            var child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            var child1Props = Props.FromProducer(() => new DisposableActor(() => child1Disposed = true))
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Props.FromProducer(() => new DisposableActor(() => child2Disposed = true))
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Props.FromProducer(() => new ParentWithMultipleChildrenActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "crash");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.True(child1Disposed);
            Assert.True(child2Disposed);
        }
    }
}