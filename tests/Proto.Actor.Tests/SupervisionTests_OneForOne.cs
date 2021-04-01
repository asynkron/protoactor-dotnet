using System;
using System.Collections.Generic;
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
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;
        private static readonly Exception Exception = new("boo hoo");

        [Fact]
        public void OneForOneStrategy_Should_ResumeChildOnFailure()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(ResumeMailbox.Instance, childMailboxStats.Posted);
            Assert.Contains(ResumeMailbox.Instance, childMailboxStats.Received);
        }

        [Fact]
        public void OneForOneStrategy_Should_StopChildOnFailure()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public void OneForOneStrategy_Should_RestartChildOnFailure()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(childMailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(childMailboxStats.Received, msg => msg is Restart);
        }

        [Fact]
        public void
            OneForOneStrategy_WhenRestartedLessThanMaximumAllowedRetriesWithinSpecifiedTimePeriod_ShouldNotStopChild()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 3,
                TimeSpan.FromMilliseconds(100)
            );
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

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
        public void
            OneForOneStrategy_WhenRestartedMoreThanMaximumAllowedRetriesWithinSpecifiedTimePeriod_ShouldStopChild()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 3,
                TimeSpan.FromMilliseconds(100)
            );
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "1st restart");
            Context.Send(parent, "2nd restart");
            Context.Send(parent, "3rd restart");
            Context.Send(parent, "4th restart");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public void OneForOneStrategy_Should_PassExceptionOnRestart()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(childMailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(childMailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
        }

        [Fact]
        public void OneForOneStrategy_Should_StopChildWhenRestartLimitReached()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");
            Context.Send(parent, "hello");

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public void OneForOneStrategy_WhenEscalateDirectiveWithoutGrandparent_ShouldRevertToDefaultDirective()
        {
            TestMailboxStatistics parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            Props childProps = Props.FromProducer(() => new ThrowOnStartedChildActor());
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            // Default directive allows 10 restarts so we expect 11 Failure messages before the child is stopped
            Assert.Equal(11, parentMailboxStats.Received.OfType<Failure>().Count());
            IEnumerable<Failure> failures = parentMailboxStats.Received.OfType<Failure>();

            // subsequent failures are wrapped in AggregateException
            foreach (Failure failure in failures)
            {
                if (failure.Reason is AggregateException ae)
                {
                    Assert.IsType<Exception>(ae.InnerException);
                }
                else
                {
                    Assert.IsType<Exception>(failure.Reason);
                }
            }
        }

        [Fact]
        public void OneForOneStrategy_Should_EscalateFailureToParent()
        {
            TestMailboxStatistics parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor());
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            Failure failure = parentMailboxStats.Received.OfType<Failure>().Single();
            Assert.IsType<Exception>(failure.Reason);
        }

        [Fact]
        public void OneForOneStrategy_Should_StopChildOnFailureWhenStarted()
        {
            TestMailboxStatistics childMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            Props childProps = Props.FromProducer(() => new ThrowOnStartedChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            childMailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, childMailboxStats.Posted);
            Assert.Contains(Stop.Instance, childMailboxStats.Received);
        }

        [Fact]
        public void OneForOneStrategy_Should_RestartParentOnEscalateFailure()
        {
            TestMailboxStatistics parentMailboxStats = new TestMailboxStatistics(msg => msg is Restart);
            OneForOneStrategy strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 0, null);
            Props childProps = Props.FromProducer(() => new ThrowOnStartedChildActor());
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            Props grandParentProps = Props.FromProducer(() => new ParentActor(parentProps))
                .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1,
                        TimeSpan.FromSeconds(1)
                    )
                );
            PID grandParent = Context.Spawn(grandParentProps);

            parentMailboxStats.Reset.Wait(1000);
            Thread.Sleep(1000); //parentMailboxStats.Received could still be modified without a wait here
            Assert.Contains(parentMailboxStats.Received, msg => msg is Restart);
        }

        private class ParentActor : IActor
        {
            private readonly Props _childProps;

            public ParentActor(Props childProps) => _childProps = childProps;

            public PID Child { get; set; }

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Started _:
                        Child = context.Spawn(_childProps);
                        break;
                    case string _:
                        context.Forward(Child);
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
