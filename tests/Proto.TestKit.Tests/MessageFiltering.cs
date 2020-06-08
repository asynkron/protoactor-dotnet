using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.TestKit.Tests
{
    public class MessageFilteringTests : TestKitBase
    {
        public MessageFilteringTests() => SetUp();

        [Fact]
        public void CanRespondToAsyncRequest()
        {
            var a = CreateTestProbe();
            var b = CreateTestProbe();

            a.Context.AtLeastOnceDelivery(b, "hi");
            b.GetNextMessage<string>(x => x.Equals("hi"));
            b.Respond(new Confirmation());

            a.ExpectNoMessage();
            b.ExpectNoMessage();
        }

        [Fact]
        public void CanDeathWatch()
        {
            var a = CreateTestProbe();
            Context.Watch(a);
            Context.Stop(a);
            GetNextMessage<Terminated>();
        }

        [Fact]
        public void FishFailsWrongType()
        {
            HundredTimes(i => Send(Probe, i));
            Send(Probe, "hi");
            HundredTimes(i => Send(Probe, i));

            this.Invoking(x => FishForMessage<DateTime>())
                .Should().Throw<Exception>().WithMessage("Message not found");
        }

        [Fact]
        public void FishFailsMissing()
        {
            HundredTimes(i => Send(Probe, i));
            Send(Probe, "hi");
            HundredTimes(i => Send(Probe, i));

            this.Invoking(_ => FishForMessage<string>(x => x.Equals("bye")))
                .Should().Throw<TestKitException>().WithMessage("Message not found");
        }

        [Fact]
        public void FishSucceedsType()
        {
            HundredTimes(i => Send(Probe, i));
            Send(Probe, "hi");
            HundredTimes(i => Send(Probe, i));

            FishForMessage<string>();
        }

        [Fact]
        public void FishSucceedsCondition()
        {
            HundredTimes(i => Send(Probe, i));
            Send(Probe, "hi");
            HundredTimes(i => Send(Probe, i));

            FishForMessage<string>(x => x.Equals("hi"));
        }

        [Fact]
        public void GetMovesToNextMessage()
        {
            HundredTimes(i => Send(Probe, i));
            HundredTimes(i => GetNextMessage<int>(x => x == i));
        }

        [Fact]
        public void GetSucceeds()
        {
            Send(Probe, "hi");
            GetNextMessage<string>();
        }

        [Fact]
        public void GetFailsWrongType()
        {
            Send(Probe, "hi");
            this.Invoking(_ => GetNextMessage<DateTime>())
                .Should().Throw<TestKitException>().WithMessage("Message expected type System.DateTime, actual type System.String");
        }

        [Fact]
        public void GetFailsNoMessage() => this.Invoking(_ => GetNextMessage<DateTime>())
            .Should().Throw<TestKitException>().WithMessage("Waited 1 seconds but failed to receive a message");

        [Fact]
        public void GetFailsCondition()
        {
            Send(Probe, "hi");
            this.Invoking(_ => GetNextMessage<string>(x => x.Equals("bye")))
                .Should().Throw<TestKitException>().WithMessage("Condition not met");
        }

        [Fact]
        public void ExpectNoMessageFails()
        {
            Send(Probe, "hi");
            this.Invoking(_ => ExpectNoMessage())
                .Should().Throw<TestKitException>().WithMessage("Waited 1 seconds and received a message of type Proto.TestKit.MessageAndSender");
        }

        [Fact]
        public void ExpectNoMessageSucceeds() => ExpectNoMessage();

        private static void HundredTimes(Action<int> runMe)
        {
            for (var i = 0; i < 100; i++)
            {
                runMe(i);
            }
        }
    }
}
