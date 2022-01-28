using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests_OneForOne
    {
        private static readonly Exception Exception = new("boo hoo");

        [Fact]
        public async Task OneForOneStrategy_Should_ResumeChildOnFailure()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(ResumeMailbox.Instance, childMailboxStats.Posted);
            Assert.Contains(ResumeMailbox.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_StopChildOnFailure()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_RestartChildOnFailure()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(childMailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(childMailboxStats.Received, msg => msg is Restart);
        }

        [Fact]
        public async Task OneForOneStrategy_WhenRestartedLessThanMaximumAllowedRetriesWithinSpecifiedTimePeriod_ShouldNotStopChild()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 3,
                TimeSpan.FromMilliseconds(100)
            );
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "1st restart");
            Context.Send(parent, "2nd restart");
            Context.Send(parent, "3rd restart");

            // wait more than the time period 
            Thread.Sleep(500);
            Assert.DoesNotContain(Stop.Instance, childMailboxStats.Posted);
            Assert.DoesNotContain(Stop.Instance, childMailboxStats.Received);

            Context.Send(parent, "4th restart");

            childMailboxStats.Reset.Wait(500);
            Assert.DoesNotContain(Stop.Instance, childMailboxStats.Posted);
            Assert.DoesNotContain(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_WhenRestartedMoreThanMaximumAllowedRetriesWithinSpecifiedTimePeriod_ShouldStopChild()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 3,
                TimeSpan.FromMilliseconds(100)
            );
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "1st restart");
            Context.Send(parent, "2nd restart");
            Context.Send(parent, "3rd restart");
            Context.Send(parent, "4th restart");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_PassExceptionOnRestart()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(childMailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(childMailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_StopChildWhenRestartLimitReached()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");
            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_WhenEscalateDirectiveWithoutGrandparent_ShouldRevertToDefaultDirective()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            var childProps = Props.FromProducer(() => new ThrowOnStartedChildActor());
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            // Default directive allows 10 restarts so we expect 11 Failure messages before the child is stopped
            Assert.Equal(11, parentMailboxStats.Received.OfType<Failure>().Count());
            var failures = parentMailboxStats.Received.OfType<Failure>();

            // subsequent failures are wrapped in AggregateException
            foreach (var failure in failures)
            {
                if (failure.Reason is AggregateException ae)
                    Assert.IsType<Exception>(ae.InnerException);
                else
                    Assert.IsType<Exception>(failure.Reason);
            }
        }

        [Fact]
        public async Task OneForOneStrategy_Should_EscalateFailureToParent()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor());
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            var parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            var failure = parentMailboxStats.Received.OfType<Failure>().Single();
            Assert.IsType<Exception>(failure.Reason);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_StopChildOnFailureWhenStarted()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            var childProps = Props.FromProducer(() => new ThrowOnStartedChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            Context.Spawn(parentProps);

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public async Task OneForOneStrategy_Should_RestartParentOnEscalateFailure()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var parentMailboxStats = new TestMailboxStatistics(msg => msg is Restart);
            var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 0, null);
            var childProps = Props.FromProducer(() => new ThrowOnStartedChildActor());
            var parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            var grandParentProps = Props.FromProducer(() => new ParentActor(parentProps))
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1,
                        TimeSpan.FromSeconds(1)
                    )
                );
            Context.Spawn(grandParentProps);

            parentMailboxStats.Reset.Wait(1000);
            Thread.Sleep(1000); //parentMailboxStats.Received could still be modified without a wait here
            Assert.Contains(parentMailboxStats.Received, msg => msg is Restart);
        }

        private class ParentActor : IActor
        {
            private readonly Props _childProps;

            public ParentActor(Props childProps) => _childProps = childProps;

            public PID? Child { get; set; }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        Child = context.Spawn(_childProps);
                        break;
                    case string _:
                        context.Forward(Child!);
                        break;
                }

                return Task.CompletedTask;
            }
        }

        private class ChildActor : IActor
        {
            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string _:
                        throw Exception;
                }

                return Task.CompletedTask;
            }
        }

        private class ThrowOnStartedChildActor : IActor
        {
            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        throw new Exception("in started");
                }

                return Task.CompletedTask;
            }
        }
    }
}