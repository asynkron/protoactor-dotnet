using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests_AllForOne
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;
        private static readonly Exception Exception = new("boo hoo");

        [Fact]
        public void AllForOneStrategy_Should_ResumeChildOnFailure()
        {
            TestMailboxStatistics child1MailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            TestMailboxStatistics child2MailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            AllForOneStrategy strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Resume, 1, null);
            Props child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            Props child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(5000);
            Assert.Contains(ResumeMailbox.Instance, child1MailboxStats.Posted);
            Assert.Contains(ResumeMailbox.Instance, child1MailboxStats.Received);
            Assert.DoesNotContain(ResumeMailbox.Instance, child2MailboxStats.Posted);
            Assert.DoesNotContain(ResumeMailbox.Instance, child2MailboxStats.Received);
        }

        [Fact]
        public void AllForOneStrategy_Should_StopAllChildrenOnFailure()
        {
            TestMailboxStatistics child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            TestMailboxStatistics child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            AllForOneStrategy strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            Props child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            Props child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, child1MailboxStats.Posted);
            Assert.Contains(Stop.Instance, child1MailboxStats.Received);
            Assert.Contains(Stop.Instance, child2MailboxStats.Posted);
            Assert.Contains(Stop.Instance, child2MailboxStats.Received);
        }

        [Fact]
        public void AllForOneStrategy_Should_RestartAllChildrenOnFailure()
        {
            TestMailboxStatistics child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            TestMailboxStatistics child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            AllForOneStrategy strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            Props child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            Props child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(child1MailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(child1MailboxStats.Received, msg => msg is Restart);
            Assert.Contains(child2MailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(child2MailboxStats.Received, msg => msg is Restart);
        }

        [Fact]
        public void AllForOneStrategy_Should_PassExceptionOnRestart()
        {
            TestMailboxStatistics child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            TestMailboxStatistics child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            AllForOneStrategy strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            Props child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            Props child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            Props parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(child1MailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child1MailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child2MailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child2MailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
        }

        [Fact]
        public void AllForOneStrategy_Should_EscalateFailureToParent()
        {
            TestMailboxStatistics parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            AllForOneStrategy strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            Props childProps = Props.FromProducer(() => new ChildActor());
            Props parentProps = Props.FromProducer(() => new ParentActor(childProps, childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            PID parent = Context.Spawn(parentProps);

            Context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            Failure failure = parentMailboxStats.Received.OfType<Failure>().Single();
            Assert.IsType<Exception>(failure.Reason);
        }

        private class ParentActor : IActor
        {
            private readonly Props _child1Props;
            private readonly Props _child2Props;

            public ParentActor(Props child1Props, Props child2Props)
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
                    context.Forward(Child1);
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
    }
}
