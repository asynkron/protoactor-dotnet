using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTestsAllForOne
    {
        private static readonly Exception Exception = new("boo hoo");

        [Fact]
        public async Task AllForOneStrategy_Should_ResumeChildOnFailure()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var child1MailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is ResumeMailbox);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Resume, 1, null);
            var child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = context.Spawn(parentProps);

            context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(5000);
            Assert.Contains(ResumeMailbox.Instance, child1MailboxStats.Posted);
            Assert.Contains(ResumeMailbox.Instance, child1MailboxStats.Received);
            Assert.DoesNotContain(ResumeMailbox.Instance, child2MailboxStats.Posted);
            Assert.DoesNotContain(ResumeMailbox.Instance, child2MailboxStats.Received);
        }

        [Fact]
        public async Task AllForOneStrategy_Should_StopAllChildrenOnFailure()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);
            var child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = context.Spawn(parentProps);

            context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(Stop.Instance, child1MailboxStats.Posted);
            Assert.Contains(Stop.Instance, child1MailboxStats.Received);
            Assert.Contains(Stop.Instance, child2MailboxStats.Posted);
            Assert.Contains(Stop.Instance, child2MailboxStats.Received);
        }

        [Fact]
        public async Task AllForOneStrategy_Should_RestartAllChildrenOnFailure()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            var child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = context.Spawn(parentProps);

            context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(child1MailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(child1MailboxStats.Received, msg => msg is Restart);
            Assert.Contains(child2MailboxStats.Posted, msg => msg is Restart);
            Assert.Contains(child2MailboxStats.Received, msg => msg is Restart);
        }

        [Fact]
        public async Task AllForOneStrategy_Should_PassExceptionOnRestart()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var child1MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var child2MailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Restart, 1, null);
            var child1Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child1MailboxStats));
            var child2Props = Props.FromProducer(() => new ChildActor())
                .WithMailbox(() => UnboundedMailbox.Create(child2MailboxStats));
            var parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
                .WithChildSupervisorStrategy(strategy);
            var parent = context.Spawn(parentProps);

            context.Send(parent, "hello");

            child1MailboxStats.Reset.Wait(1000);
            child2MailboxStats.Reset.Wait(1000);
            Assert.Contains(child1MailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child1MailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child2MailboxStats.Posted, msg => msg is Restart r && r.Reason == Exception);
            Assert.Contains(child2MailboxStats.Received, msg => msg is Restart r && r.Reason == Exception);
        }

        [Fact]
        public async Task AllForOneStrategy_Should_EscalateFailureToParent()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var parentMailboxStats = new TestMailboxStatistics(msg => msg is Stopped);
            var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Escalate, 1, null);
            var childProps = Props.FromProducer(() => new ChildActor());
            var parentProps = Props.FromProducer(() => new ParentActor(childProps, childProps))
                .WithChildSupervisorStrategy(strategy)
                .WithMailbox(() => UnboundedMailbox.Create(parentMailboxStats));
            var parent = context.Spawn(parentProps);

            context.Send(parent, "hello");

            parentMailboxStats.Reset.Wait(1000);
            var failure = parentMailboxStats.Received.OfType<Failure>().Single();
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

            private PID? Child1 { get; set; }
            private PID? Child2 { get; set; }

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
                    context.Forward(Child1!);
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